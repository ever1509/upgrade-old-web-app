# Where this project currently stands

Living status page. Update it as phases complete.

**Last updated:** 2026-08-31

---

## The short version

The migration has **not started**. Everything so far is preparation, and that is
deliberate: nothing targets .NET 10 yet, and nothing should until the assessment
is written down.

What exists is a working .NET Framework 4.8 application, a test suite that
describes its behaviour, and three findings about why it cannot stay where it is.

## Phase status

| Phase | State | Notes |
|---|---|---|
| 1. Build the legacy app | Done | Runs in Windows 11 under Parallels, VS 2026 |
| 2. Characterization tests | Mostly | 72 rule tests green; EF integration tests not written |
| 3. Assessment / ledger | **Next** | Findings below are the raw material |
| 4. De-risk in place | Barely started | Transport swap done early, out of necessity |
| 5. Strangler cutover | Not started | |
| 6. Delete the old app | Not started | |
| 7. Modernise | Not started | |
| 8. PostgreSQL (optional) | Not started | |

## What actually runs today

| | |
|---|---|
| Web app | MVC 5 + Web API 2 + SignalR 2 on IIS Express, port 52080 |
| Database | SQL Server Express 2014, `.\SQLEXPRESS`, Windows auth |
| Queue | File-based, `C:\ExpenseFlow\queue` (MSMQ is not installable) |
| Worker | Console mode; thumbnails, PDF, email, notifications |
| Tests | `dotnet test` / VS Test Explorer, 72 passing |

Verified by hand: authentication, all claim submission rules, approval and
rejection including the self-approval block and the 500 senior-approval
threshold, the Web API endpoints, the reports page, and the audit module.

## Findings log

The seed of the phase 3 ledger. These are things learned by running the app,
not predicted on paper — which is what makes them worth writing down.

### F-1. MSMQ cannot be installed on current Windows

`Enable-WindowsOptionalFeature -FeatureName MSMQ-Server` fails with
`Feature name MSMQ-Server is unknown`. Only `WCF-MSMQ-Activation45` is present.
Microsoft has been deprecating MSMQ, and Windows on ARM ships a reduced feature
set.

**Significance:** the dependency is not merely unsupported on .NET Core; the
operating system will no longer host it at all. There is no "leave it as is"
option.

**Response:** replaced with a file-based queue as a bridge (done); RabbitMQ at
phase 4, after `packages.config` becomes `PackageReference`.

**Cost to swap:** two new classes. Zero lines changed in the web app or the
worker's message handling, because both depend on `IMessagePublisher` /
`IMessageReceiver` rather than on a transport. Cheapest blocker in the codebase
to remove, and the deadest — the correlation is the lesson.

### F-2. LocalDB cannot be used from the web app on this machine

`EntityException: The underlying provider failed on Open`, inner
`Win32Exception: '%1 is not a valid Win32 application'`.

.NET Framework's `SqlClient` starts a LocalDB instance by loading
`sqluserinstance.dll` *into the calling process*, so IIS Express and LocalDB must
share a CPU architecture. Confusingly `sqlcmd` still works, being a separate
binary whose architecture happens to match.

**Significance:** an in-process native dependency hidden inside something that
looks like a connection string.

**Response:** switched to SQL Server Express reached over a socket, where
architecture is irrelevant. One connection-string change.

### F-3. Three Windows-only APIs still untested against the migration

`System.Drawing` (thumbnails), PdfSharp 1.50 over GDI+ (claim PDFs), and
`SmtpClient` (email). All three now execute, so a baseline exists.

`System.Drawing` throws on non-Windows since .NET 6, and PdfSharp 1.50 is
GDI+-backed. `SmtpClient` will cross over with only an obsolescence warning —
worth recording, because knowing what does *not* block you is half of an
assessment.

### Cross-cutting observation

The dependencies that hurt are the ones reached for **directly**:
`HttpContext.Current`, `FormsAuthentication`, `System.Drawing`,
`GlobalHost.ConnectionManager`. The ones behind an interface cost almost nothing
to replace, regardless of how obsolete they are. That is the argument for the
whole design of the migration, and it was demonstrated rather than asserted.

## Next actions

1. **Phase 3 — write the ledger.** Run `upgrade-assistant analyze`, grep the
   blast radius of `CurrentUser.`, and produce a decision per dependency.
2. **First .NET 10 artifact** — multi-target `ExpenseFlow.Domain` as
   `net48;net10.0`. Already verified to compile and pass all 72 tests on .NET 10.
   This is the point the work can move to Rider on macOS.
3. **Finish phase 2** — EF integration tests against SQL Server Express.
4. **Phase 4 proper** — `packages.config` to `PackageReference`, SDK-style
   projects, then RabbitMQ.
