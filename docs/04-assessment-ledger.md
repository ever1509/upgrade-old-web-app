# Phase 3 — assessment ledger

The deliverable of the assessment phase: every dependency that stands between
this application and .NET 10, with a decision attached to each one.

Written before any porting begins. Nothing here is speculative effort — the
blast radius numbers are measured from the source, and three of the findings
came from running the app rather than reading it.

**Target:** .NET 10 (LTS). **Method:** incremental strangler fig, never big-bang.

---

## Method

1. Measure how many files touch each dependency. Effort tracks blast radius far
   more closely than it tracks how obsolete something is.
2. Separate *blockers* (the port cannot happen until this is solved) from
   *carry-over* items (they compile on .NET 10 and can be modernised later).
   Knowing what does **not** block you is half the value of an assessment.
3. Sequence by risk, not by enthusiasm. Cheap and isolated first, auth last.

Still to run, in Windows:

```powershell
dotnet tool install -g upgrade-assistant
upgrade-assistant analyze .\ExpenseFlow.sln
```

Its output should be reconciled against this table rather than replace it — the
tool finds API-level problems well and architectural ones badly.

## Blast radius, measured

| Dependency | Files | Uses |
|---|---:|---:|
| SignalR (hub, client, config) | 9 | 30 |
| `ActionResult` / MVC 5 types | 8 | 25 |
| `ViewBag` / `dynamic` | 10 | 22 |
| EF6 `.Include(...)` | 3 | 17 |
| `FormsAuthentication` | 2 | 13 |
| `CurrentUser` (ambient static) | 6 | 11 |
| EF6 `DbContext` / `DbSet` | 3 | 11 |
| `System.Drawing` / GDI+ | 3 | 10 |
| `HttpContext.Current` | 4 | 9 |
| Web API 2 `ApiController` | 2 | 8 |
| `SmtpClient` / `MailMessage` | 2 | 4 |
| `ConfigurationManager` | 2 | 3 |
| `Server.MapPath` | 1 | 3 |
| `Session[...]` | 0 | 0 |

`Session` scoring zero is worth noting: session state is configured in
`web.config` but never actually used. That removes the single hardest problem in
side-by-side migration — sharing session between the old and new app — and means
the System.Web adapters can be configured without session bridging.

## Portability by project

| Project | Files / lines | Windows-only references | Can move |
|---|---|---|---|
| `ExpenseFlow.Domain` | 17 / 578 | none | **Today.** Verified: compiles on .NET 10, all 72 tests pass. |
| `ExpenseFlow.Data` | 18 / 570 | none | **Today**, once EF6 becomes EF Core. Verified to compile on .NET 10 against EF6's netstandard build. |
| `ExpenseFlow.Messaging` | 13 / 616 | `System.Messaging` | Once the MSMQ classes are dropped. The file transport is already clean. |
| `ExpenseFlow.Worker` | 10 / 762 | `System.Drawing`, `System.Messaging`, `System.ServiceProcess`, `System.Configuration.Install` | After three replacements. No `System.Web` at all — the first migration slice. |
| `ExpenseFlow.Web` | 24 / 1455 + 10 views | 17 `System.Web*` assemblies, `System.Drawing` | Last. This is the strangler target. |

## The ledger

Effort is T-shirt sized against blast radius. Risk is the chance of silently
changing behaviour, which is not the same as difficulty.

### Blockers

| # | Dependency | Replacement | Effort | Risk | Slice |
|---|---|---|---|---|---|
| B1 | MSMQ (`System.Messaging`) | RabbitMQ | S | L | 4 |
| B2 | `ServiceBase` Windows Service | `BackgroundService` on the generic host | S | L | 1 |
| B3 | `System.Drawing` / GDI+ | ImageSharp | S | L | 1 |
| B4 | PdfSharp 1.50 (GDI+) | QuestPDF | M | M | 1 |
| B5 | EF6 | EF Core 10 | M | **H** | 3 |
| B6 | `HttpContext.Current` via `CurrentUser` | `IHttpContextAccessor`, or an explicit parameter | M | **H** | 4 |
| B7 | MVC 5 + Web API 2 as two stacks | one `ControllerBase` model | M | M | 3–4 |
| B8 | SignalR 2.x | ASP.NET Core SignalR | M | M | 5 |
| B9 | `FormsAuthentication` | cookie authentication | M | **H** | 6 |
| B10 | `Global.asax` lifecycle events | middleware + `Program.cs` | S | M | 4 |
| B11 | `IHttpModule` (audit) | middleware | S | L | 4 |
| B12 | `System.Web.Optimization` bundling | static assets or a real front-end build | S | L | 4 |
| B13 | Salted SHA-256, one round | PBKDF2, rehash on next login | S | M | 6 |

### Carry-over — compiles on .NET 10, modernise later

| # | Item | Note |
|---|---|---|
| C1 | `SmtpClient` | Obsolete warning only. MailKit is phase 7, not a blocker. |
| C2 | Stored procedures via `SqlQuery<T>` | API renamed; the SQL is untouched. |
| C3 | `ConfigurationManager` / static `AppSettings` | Becomes `IOptions<ApprovalPolicy>`. Only 3 uses. |
| C4 | `Server.MapPath` | One file. Becomes `IWebHostEnvironment`. |
| C5 | `MAX+1` claim numbering | Racy under concurrency. A correctness bug, not a migration one. Fix after. |
| C6 | Dual write in `ClaimsController.Submit` | `SaveChanges` and `Publish` are separate transactions. Transactional outbox, phase 5. |
| C7 | EF6 on non-Framework targets pulls `System.Drawing.Common` 4.7.0 | NuGet flags it as a **known critical vulnerability** (GHSA-rxg9-xrhp-64gj). Harmless while EF6 is only a transitional state, but it must not survive into production. Another reason B5 (EF Core) is not optional. |
| C8 | `log4net` 2.0.15 flagged by NuGet audit | Known moderate severity vulnerability (GHSA-4f7c-pmjv-c25w). Independent of the migration - it wants upgrading regardless - but the migration is the natural moment to do it. |

### Security posture

Two of the three vulnerability warnings NuGet raises against this solution come
from dependencies that are simply old, not from anything the migration
introduces. Recording them here because "we upgraded the framework" is the only
moment anyone will fund fixing them, and because a legacy codebase's security
debt is part of the honest case for migrating:

* `System.Drawing.Common` 4.7.0 - **critical**, arrives transitively with EF6
* `log4net` 2.0.15 - **moderate**

## Why B5, B6 and B9 are the high-risk three

They are high risk for the same reason: **they fail quietly.**

- **EF6 to EF Core** — LINQ that EF6 translated to SQL may silently switch to
  client-side evaluation, or throw only on a query path nobody exercises. The 17
  `.Include(...)` calls and the string-mapped `Status` column are the specific
  hazards.
- **`HttpContext.Current`** — 6 files, 11 uses, and it currently opens its own
  `DbContext` per request. Get the lifetime wrong and one user sees another
  user's data. That is the worst failure mode in the whole application, and it
  fails silently.
- **Forms Auth** — touches every request. Password hashes must keep validating
  across the change or every user is locked out at once.

Everything else announces itself at compile time. These three do not, which is
what the characterization tests exist for.

## Sequencing

| Slice | What | Why here |
|---|---|---|
| 1 | The worker | Zero `System.Web`. Ship it before touching a web route. Runs on macOS immediately. |
| 2 | Admin reports | Read-only, tiny surface. Proves the YARP seam with nothing at stake. |
| 3 | Web API 2 endpoints | Mechanical, no Razor. |
| 4 | Claims + Approvals MVC | The bulk. `HttpContext.Current`, EF Core, middleware. |
| 5 | SignalR | The worker can then hold `IHubContext` directly, deleting the internal HTTP callback entirely. |
| 6 | Forms Auth | Last. Touches everything, fails worst. |

Preceded by phase 4: `packages.config` to `PackageReference`, SDK-style
projects, multi-target `Domain`/`Data`/`Messaging`, then RabbitMQ.

## Findings that changed the plan

Three things learned by running the application, not by reading it.

**MSMQ is not installable on current Windows.** Not deprecated-but-working —
absent. The dependency could not have been left alone even if the migration were
cancelled. Replacement was pulled forward from phase 4 to phase 1.

**LocalDB could not be used from the web app.** `SqlClient` starts LocalDB by
loading `sqluserinstance.dll` into the calling process, so IIS Express and
LocalDB must share a CPU architecture. `sqlcmd` worked throughout, which made it
look like an application bug. An in-process native dependency was hiding inside
something that looked like a connection string.

**Obsolescence does not predict cost.** MSMQ was the deadest dependency in the
codebase and the cheapest to remove: two new classes, **zero** lines changed in
the web app or the worker's message handling, because both depended on
`IMessagePublisher` rather than on a transport. `HttpContext.Current` is alive,
supported, and will be among the most expensive things here — 6 files reaching
for it directly.

What predicts cost is not age. It is whether the dependency was reached for
directly or sat behind a seam.
