# ExpenseFlow — a .NET Framework → .NET 10 migration exercise

A deliberately period-accurate **ASP.NET MVC 5 + Web API 2 line-of-business app**
targeting **.NET Framework 4.8**, built as the *starting point* for a realistic,
step-by-step migration to **.NET 10**.

The app itself is boring on purpose: employees file expense claims, managers
approve them, Finance reports on them. What matters is that every screen is
wired to a dependency that a real migration has to deal with.

---

## Current state

The migration has not started yet — see
[docs/00-progress.md](docs/00-progress.md) for exactly where things stand and
what was learned so far.

## Phase 1: complete — build and run it on Windows

Start here: [docs/01-windows-setup.md](docs/01-windows-setup.md).
Then prove it works: [docs/02-verification-checklist.md](docs/02-verification-checklist.md).
The road ahead: [docs/03-migration-plan.md](docs/03-migration-plan.md).
The assessment: [docs/04-assessment-ledger.md](docs/04-assessment-ledger.md).

---

## What's in the box

```
ExpenseFlow.sln
├── src/ExpenseFlow.Domain      net48  entities + pure business rules   ← ports unchanged
├── src/ExpenseFlow.Data        net48  EF6 DbContext, mappings, repos   ← mostly mechanical
├── src/ExpenseFlow.Messaging   net48  MSMQ publisher/receiver          ← blocker
├── src/ExpenseFlow.Web         net48  MVC 5 + Web API 2 + SignalR 2    ← strangler target
├── src/ExpenseFlow.Worker      net48  Windows Service consumer         ← blocker
└── db/                                schema, seed data, stored procs
```

### The one user story that carries the whole exercise

> An employee submits a claim → a message goes on **MSMQ** → a **Windows Service**
> picks it up → renders receipt thumbnails with **System.Drawing** → builds a PDF
> with **PdfSharp/GDI+** → emails the approver with **SmtpClient** → pushes a live
> toast through **SignalR 2**.

That is five Windows-only or legacy dependencies in a single flow. None of them
can follow you to macOS. That's the point: the OS boundary does the teaching,
and you can't hand-wave your way past it.

## The deliberate legacy inventory

| What | Where | Verdict | Notes |
|---|---|---|---|
| MSMQ (`System.Messaging`) | `ExpenseFlow.Messaging` | Blocker | Never ported to .NET Core. Full replacement. |
| Windows Service (`ServiceBase`) | `Worker/WorkerService.cs` | Blocker | Becomes a `BackgroundService` |
| System.Drawing / GDI+ | `Worker/Handlers/ThumbnailRenderer.cs` | Blocker | Throws on non-Windows since .NET 6 |
| PdfSharp 1.50 (GDI+ backed) | `Worker/Handlers/ClaimPdfWriter.cs` | Blocker | Replace with QuestPDF or PDFsharp 6 |
| `HttpContext.Current` static | `Web/Security/CurrentUser.cs` | Blocker | Doesn't exist. Grep it, the blast radius is large. |
| Forms Authentication | `Web/Controllers/AccountController.cs` | Blocker | Becomes cookie auth. Migrate this last. |
| `Global.asax` lifecycle events | `Web/Global.asax.cs` | Significant | Becomes middleware plus `Program.cs` |
| `IHttpModule` | `Web/Modules/AuditLogModule.cs` | Significant | Becomes middleware |
| SignalR 2.x | `Web/Hubs/NotificationHub.cs` | Significant | New client, no `/signalr/hubs` proxy |
| MVC 5 and Web API 2 side by side | `Web/Controllers/**` | Significant | Two stacks collapse into one |
| EF6, string enums, lazy proxies | `ExpenseFlow.Data` | Significant | EF Core value converters |
| `web.config` + static `AppSettings` | `Web/App_Start/AppSettings.cs` | Minor | Becomes `IOptions<T>` |
| `System.Web.Optimization` bundling | `Web/App_Start/BundleConfig.cs` | Minor | Static assets or a real front-end build |
| `packages.config` | every project | Minor | First mechanical step |
| `Server.MapPath` file storage | `Web/Security/ReceiptStorage.cs` | Minor | Becomes `IWebHostEnvironment` |
| Salted SHA-256, one round | `Web/Security/PasswordHasher.cs` | Minor | Security finding. Move to PBKDF2. |
| `SmtpClient` | `Worker/Handlers/EmailSender.cs` | Crosses over | Obsolete warning only. MailKit later. |
| Stored procs via `SqlQuery<T>` | `Data/Repositories/ReportRepository.cs` | Crosses over | API renamed, the SQL survives |
| MAX+1 claim numbering | `Data/Repositories/ClaimRepository.cs` | Crosses over | Racy. Fix after the migration. |

The last three rows matter as much as the first: knowing what *doesn't* block
you is half of a good assessment.

### The counterweight

[`ClaimWorkflow.cs`](src/ExpenseFlow.Domain/Workflow/ClaimWorkflow.cs) holds every
business rule as pure, framework-free static methods — no `System.Web`, no EF, no
config. It's the anchor of the whole exercise:

* it's what the **phase 2 characterization tests** pin down;
* it's the **first thing that compiles unchanged on .NET 10**;
* it's the contrast that shows *why* everything else hurts.

## Seeded accounts

Password for all: `Passw0rd!`

| Email | Role | Notes |
|---|---|---|
| `alice@expenseflow.local` | Employee | reports to Bob; starts with one draft claim |
| `bob@expenseflow.local` | Approver | manages Alice, Carla, Erik; reports to Dana |
| `carla@expenseflow.local` | Employee | Sales |
| `erik@expenseflow.local` | Employee | Engineering |
| `dana@expenseflow.local` | Admin | Finance — the only role that can decide claims ≥ 500 |

## Key business rules (worth reading before you run it)

* A claim needs a title and ≥ 1 line, every line > 0.00, no future dates.
* Categories have a **receipt threshold** — e.g. Meals allows 25.00 without a
  receipt, Taxi 15.00, Travel/Hotel always require one.
* **You cannot decide your own claim.**
* An approver may only decide claims of their **own direct reports**; an Admin
  may decide anyone's.
* Claims **≥ 500 must be decided by an Admin**, not just any approver.
* Rejected claims return to editable state and can be resubmitted, keeping their
  full audit trail.
* Only an Admin can mark an approved claim reimbursed.

## Ground rules for the migration

1. **One axis at a time.** Never change framework *and* queue *and* database *and*
   OS in the same move. When something breaks you must know which change did it.
2. **Tests before changes.** Phase 2 exists so that every later phase has a
   safety net. Skipping it makes the whole thing faith-based.
3. **Strangler fig, never big-bang.** Old and new run side by side behind YARP;
   routes move one at a time; you can stop and ship at any point.
4. **Auth goes last.** It touches every request and has the nastiest edge cases.
5. **Write the ledger down.** Phase 3 produces a written inventory of every
   blocker with a decision attached. That document is the artifact that makes you
   able to *talk* about the migration, not just perform it.

## Target

**.NET 10 (LTS)**, ASP.NET Core, EF Core, RabbitMQ, a Worker Service, and
JetBrains Rider on macOS. SQL Server stays the database throughout; PostgreSQL is
an optional victory lap once EF Core makes it a provider swap.
