# The migration plan — .NET Framework 4.8 → .NET 10

Written up front so you know where every phase is going. Follow Microsoft's
**incremental (strangler fig)** guidance: old and new run side by side, routes
move one at a time, and you can stop and ship at any point.

**Never big-bang.** A rewrite-and-swap of an app this size is where migrations go
to die.

---

## The one rule

> **Change one axis at a time.** Framework, queue technology, database, and
> operating system are four independent variables. Move one, verify, commit.
> When something breaks you must know which change did it.

This is why MSMQ→RabbitMQ happens in phase 4 while still on .NET Framework in
Windows, and not simultaneously with the .NET 10 port.

---

## Phase 1 — the legacy app (done)

Build it, run it, verify it. See [01-windows-setup.md](01-windows-setup.md) and
[02-verification-checklist.md](02-verification-checklist.md).

## Phase 2 — characterization tests (the safety net)

**Still .NET Framework. Still Windows. No production code changes.**

Add `tests/ExpenseFlow.Tests` targeting `net48` and pin down the behaviour you
recorded in phase 1 — the exact validation strings included.

Target `ClaimWorkflow` first: it's pure, has no framework dependencies, and
holds every rule that matters. Aim for complete coverage of:

- submit validation (empty claim, zero amounts, future dates, receipt thresholds, max amount, max lines)
- `CanDecide` (own claim, wrong manager, senior threshold, wrong status)
- `CanReimburse`, `CanEdit`, `CanView`
- the resubmit-after-rejection transition and its history entries

Then add a thin integration layer over `ExpenseFlowContext` against a
throwaway LocalDB, covering `NextClaimNumber` and the two stored procedures.

> **Do not skip this phase.** Everything after it is only safe because this
> exists. It is also the single most convincing thing you can point at when
> explaining how you did the migration with confidence.

**Exit criteria:** a green test run you trust, that fails loudly if a rule changes.

## Phase 3 — assessment (write the ledger down)

**Still .NET Framework. Still Windows. Still no code changes.**

```powershell
dotnet tool install -g upgrade-assistant
upgrade-assistant analyze .\ExpenseFlow.sln
```

Also run the .NET Portability/API analyzers, then produce a written **migration
ledger** — one row per dependency, each with a decision:

| Dependency | Verdict | Replacement | Effort | Risk |
|---|---|---|---|---|
| `System.Messaging` (MSMQ) | Blocker | RabbitMQ | M | M |
| `ServiceBase` | Blocker | `BackgroundService` | S | L |
| `System.Drawing` | Blocker | ImageSharp | S | L |
| PdfSharp 1.50 | Blocker | QuestPDF | M | M |
| `HttpContext.Current` | Blocker | `IHttpContextAccessor` | M | H |
| Forms Auth | Blocker | cookie auth | L | H |
| `SmtpClient` | Crosses over | (MailKit later) | — | — |
| … | | | | |

Start it by grepping the blast radius of the worst offender:

```powershell
findstr /S /N /C:"CurrentUser." src\*.cs src\*.cshtml
```

This document is the real deliverable of the whole exercise. It's what turns
"I migrated an app" into "here's how I assessed and sequenced a migration."

**Exit criteria:** every blocker has a named replacement and a decision attached.

## Phase 4 — de-risk in place (the macOS crossing)

**Still .NET Framework. The last phase that is Windows-only.**

In order:

1. **`packages.config` → `PackageReference`** in every project. Mechanical; unblocks everything else.
2. **Old `.csproj` → SDK-style.** The web project stays last and stays messy.
3. **Multi-target** `Domain`, `Data`, `Messaging` as `net48;net10.0`. Fix what won't compile under `net10.0`. `Domain` should be nearly free — that's the payoff for keeping it pure.
4. **MSMQ → RabbitMQ**, *while still on .NET Framework*. `RabbitMQ.Client` supports net472+, so the existing app and the existing tests keep working. Only `MsmqMessagePublisher`/`MsmqMessageReceiver` change — `IMessagePublisher` was the seam that made this cheap.
5. **SQL Server LocalDB → Developer Edition** with TCP enabled, so something outside the VM can reach it.

### The crossing

Once step 3 lands, `Domain`, `Data` and `Messaging` build on .NET 10 — so they
build in **Rider on macOS**. From here:

- **RabbitMQ** runs natively on your arm64 Mac: `docker run -d --name rabbit -p 5672:5672 -p 15672:15672 rabbitmq:3-management`
- **SQL Server** stays in the Parallels VM; connect from macOS over the Parallels network (`Get-NetIPAddress` in the VM for the IP). The official SQL Server image is amd64-only, so running it on the Mac means Rosetta emulation — keep the VM's instance instead.
- The **web app stays in Windows** for now. That's expected: it's the last thing to move.

**Exit criteria:** you can open the solution in Rider on macOS, build the three
library projects, and run the phase 2 tests green — on the Mac.

## Phase 5 — the strangler cutover

**New ASP.NET Core 10 host in front, old app still serving everything else.**

Stand up:

- a new `ExpenseFlow.Web.Core` ASP.NET Core 10 app
- **YARP** in front, routing unmigrated paths back to the .NET Framework app
- **`Microsoft.AspNetCore.SystemWebAdapters`** to share session and auth across the two

Then move slices in this order — easiest and lowest-risk first:

| # | Slice | Why this order |
|---|---|---|
| 1 | **The worker** | Zero `System.Web`. Becomes a .NET 10 Worker Service, runs natively on macOS. Ship it before touching a single web route. |
| 2 | **Admin reports** | Read-only, tiny surface, no writes. Safe way to prove the YARP seam works. |
| 3 | **Web API 2 → Core controllers** | Mechanical; no Razor involved. |
| 4 | **Claims + Approvals MVC** | The bulk. `HttpContext.Current` → DI, EF6 → EF Core, Razor views mostly port. |
| 5 | **SignalR 2 → Core SignalR** | New client (`@microsoft/signalr`), no `/signalr/hubs` proxy, `GlobalHost` → injected `IHubContext`. The worker can now push directly — delete `NotificationPusher` and the internal endpoint entirely. |
| 6 | **Forms Auth → cookie auth** | **Last.** Touches every request. Rehash passwords to PBKDF2 on next login so users never notice. |

Along the way: `Global.asax` → `Program.cs` + middleware, `AuditLogModule` →
middleware, `web.config` → `appsettings.json` + `IOptions<ApprovalPolicy>`,
`Server.MapPath` → `IWebHostEnvironment`, string status columns → EF Core
`HasConversion<string>()`.

Also fix the **dual write** in `ClaimsController.Submit` — `SaveChanges` and
`Publish` are separate transactions today, so a crash between them submits a
claim nobody is ever told about. That's the **transactional outbox** pattern.

**Exit criteria:** every route served by .NET 10; YARP forwards nothing.

## Phase 6 — delete the old app

Remove YARP, the System.Web adapters, and the .NET Framework projects. Drop the
`net48` target. Delete the `packages\` folder.

**Exit criteria:** the whole solution builds and runs on macOS in Rider, no
Windows required.

## Phase 7 — modernise (now that you can)

The things that were *possible* before but not *worth it*: `SmtpClient` → MailKit,
DI everywhere, health checks, structured logging, `MAX+1` claim numbering → a
sequence, N+1 queries from the old lazy-loading proxies, async all the way down.

## Phase 8 — optional victory lap: PostgreSQL

With EF Core in place, swapping SQL Server → Postgres is *nearly* a provider
change. The leftovers are the interesting part: the two stored procedures, raw
SQL, `datetime2` vs `timestamptz`, identity columns, identifier case-sensitivity,
`ROWVERSION` → `xmin`.

Postgres is arm64-native in Docker, so this permanently fixes local dev on the
Mac. Good demonstration of what the migration bought you: a thing that was
impossible before is now a config change plus a short, known list of leaks.

---

## Reference

- [Incremental ASP.NET to ASP.NET Core migration](https://learn.microsoft.com/aspnet/core/migration/inc/overview)
- [System.Web adapters](https://learn.microsoft.com/aspnet/core/migration/inc/systemweb-adapters)
- [.NET Upgrade Assistant](https://learn.microsoft.com/dotnet/core/porting/upgrade-assistant-overview)
- [EF6 → EF Core porting guide](https://learn.microsoft.com/ef/efcore-and-ef6/porting/)
- [YARP](https://microsoft.github.io/reverse-proxy/)
