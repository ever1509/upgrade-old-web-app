# Phase 1 — get ExpenseFlow running in Windows 11 (Parallels)

Everything here happens **inside the Windows VM**. macOS gets involved at phase 4.

Budget ~45 minutes, most of it waiting on the Visual Studio installer.

---

## 1. Visual Studio 2026 Community

Install these two workloads:

- **ASP.NET and web development** — brings MSBuild web targets, IIS Express, Razor tooling
- **.NET desktop development** — needed for the Windows Service project

Under *Individual components*, confirm **.NET Framework 4.8 targeting pack** is
checked (the ASP.NET workload normally includes it).

> You do **not** need the old "ASP.NET Web Application (.NET Framework)" project
> template. Every `.csproj` in this repo is hand-written, so you just open the
> solution. If VS 2026 has dropped those templates, it changes nothing here.

## 2. MSMQ (expect this to fail)

The app publishes to MSMQ, which is a Windows *feature*, not a NuGet package.
Try to enable it from **PowerShell as Administrator**:

```powershell
Enable-WindowsOptionalFeature -Online -FeatureName MSMQ-Server -All
```

**On a current Windows 11 this will almost certainly fail** with
`Feature name MSMQ-Server is unknown`. Confirm what your machine actually has:

```powershell
Get-WindowsOptionalFeature -Online | Where-Object FeatureName -like "*MSMQ*" | Select-Object FeatureName, State
```

If the only result is `WCF-MSMQ-Activation45`, MSMQ is not installable here.
Microsoft has been deprecating it, and Windows on ARM ships a reduced feature
set. That is the expected outcome, and it is not a problem to fix — it is the
first real finding of the exercise, so write it down for the phase 3 ledger.

Consequences, both handled:

* the web app catches the publish failure and shows *"Claim submitted, but the
  notification service could not be reached"* — the claim still submits and
  every business rule still runs
* the worker logs a fatal MSMQ error on startup and idles

### The stand-in

Rather than lose the whole background pipeline, the app ships a second
transport: a queue made of JSON files in a folder. It is selected by

```xml
<add key="ExpenseFlow:Transport" value="file" />
```

in both `Web.config` and the worker's `App.config`, and it is the default.
Set it to `msmq` on a machine that still has MSMQ.

The file queue keeps the shape that matters — the web request hands work off and
returns immediately, a separate process picks it up later — along with
oldest-first delivery, retries, dead-lettering after three failures, and
recovery of messages abandoned by a crashed worker. It is a bridge, not the
destination: phase 4 replaces it with RabbitMQ, once `packages.config` has
become `PackageReference` and transitive dependencies stop being hand-wired.

`IMessagePublisher` and `IMessageReceiver` are why this was a small change. The
web app and the worker never referenced MSMQ directly, so swapping the transport
touched neither.

## 3. Create the shared folders

The web app and the Windows Service exchange files through the filesystem, by
convention. Both configs point at these paths:

```powershell
New-Item -ItemType Directory -Force -Path C:\ExpenseFlow\uploads, C:\ExpenseFlow\pdf, C:\ExpenseFlow\mail, C:\ExpenseFlow\queue
```

- `uploads` — receipt originals + generated thumbnails
- `pdf` — generated claim PDFs
- `mail` — outgoing email as `.eml` files (no SMTP server needed)
- `queue` — the file-based message queue standing in for MSMQ

## 4. Clone the repo

```powershell
git clone <your-repo-url> C:\src\upgrade-old-web-app
```

Put it on the Windows filesystem, **not** on a Parallels shared folder pointing
at macOS. Building .NET Framework across the shared-folder bridge is slow and
occasionally produces file-locking errors.

## 5. Create the database

**Use SQL Server Express, not LocalDB.** Both `Web.config` and the worker's
`App.config` are configured for `.\SQLEXPRESS` with Windows authentication, so
there is nothing to edit.

Check what you already have — Visual Studio installs often include Express:

```powershell
Get-Service | Where-Object { $_.Name -like 'MSSQL*' } | Select-Object Name, Status
```

`MSSQL$SQLEXPRESS` means you are ready. If it is missing, install **SQL Server
Express** (or Developer Edition) and re-run the check.

Create the schema and seed data:

```powershell
sqlcmd -S ".\SQLEXPRESS" -E -i db\01_schema.sql -i db\02_seed.sql -i db\03_reporting_procs.sql
```

Confirm it worked:

```powershell
sqlcmd -S ".\SQLEXPRESS" -E -d ExpenseFlow -Q "SELECT FullName, Role FROM dbo.Employees"
```

Five people, ending with Erik Lindqvist. If `sqlcmd` is not on your PATH, open
the three files in SSMS or in Visual Studio's SQL Server Object Explorer and run
them in order.

> **Why not LocalDB?** .NET Framework's `SqlClient` starts a LocalDB instance by
> loading `sqluserinstance.dll` *into the calling process*, so IIS Express and
> LocalDB must share a CPU architecture. When they do not — which is the normal
> case on Windows on ARM — you get
> `EntityException: The underlying provider failed on Open` wrapping
> `Win32Exception: '%1 is not a valid Win32 application'`. Confusingly, `sqlcmd`
> still works, because it is a separate binary whose architecture happens to
> match. A normal SQL Server instance is a service reached over a socket, so the
> question never arises. This is the second finding for your ledger.

## 6. Restore NuGet packages

Open `ExpenseFlow.sln` in Visual Studio. Restore usually runs on first build; if
not, right-click the solution → **Restore NuGet Packages**.

These are `packages.config` projects, so packages land in `packages\` at the repo
root and are wired in via `HintPath`.

> **If restore fails on a version:** the pinned versions are the era-accurate
> ones, but NuGet is unforgiving about exact matches in `packages.config`. Bump
> the version in the project's `packages.config` *and* the matching `HintPath` in
> the `.csproj` to whatever restore offers. The most likely candidate is
> `PdfSharp 1.50.5147` in `ExpenseFlow.Worker`.

## 7. Configure multi-project startup

Right-click the **solution** → *Properties* → *Startup Project* →
**Multiple startup projects**:

| Project | Action |
|---|---|
| ExpenseFlow.Web | **Start** |
| ExpenseFlow.Worker | **Start** |
| (everything else) | None |

The worker detects it's running interactively and runs as a console app rather
than trying to register as a Windows Service. You'll get a second window showing
its log — that's where you watch the queue being drained.

## 8. Build and run

Press **F5**.

- Web app: <http://localhost:52080/>
- Worker: a console window logging `Listening on .\private$\expenseflow`

Sign in as `alice@expenseflow.local` / `Passw0rd!`

Then work through **[02-verification-checklist.md](02-verification-checklist.md)**.

---

## Optional: install the worker as a real Windows Service

Console mode is better for development, but registering it properly is worth
doing once — the Windows Service host is one of the things the migration
replaces, so it helps to have seen it work.

From an **Administrator** prompt:

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\installutil.exe C:\src\upgrade-old-web-app\src\ExpenseFlow.Worker\bin\Debug\ExpenseFlow.Worker.exe
Start-Service ExpenseFlowWorker
```

To remove it later:

```powershell
Stop-Service ExpenseFlowWorker
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\installutil.exe /u C:\src\upgrade-old-web-app\src\ExpenseFlow.Worker\bin\Debug\ExpenseFlow.Worker.exe
```

Note the service runs as **LocalSystem**, which will *not* have your user's
LocalDB instance. If you go this route, switch the connection string in
`App.config` to a real SQL Server instance — which is a nice preview of the
phase 4 database conversation.

---

## Troubleshooting

**"Could not load file or assembly System.Web.Mvc"**
Restore didn't run, or a `HintPath` doesn't match the restored version. Check
`packages\` actually contains the folder named in the `.csproj`.

**Worker logs "Could not open the MSMQ queues"**
Expected on any Windows without MSMQ — see step 2. The worker idles; nothing
else is affected.

**"The CodeDom provider type Microsoft.CodeDom.Providers.DotNetCompilerPlatform
could not be located"**
Should not happen any more — that provider was removed. If you see it, you are
on an older commit; pull.

**`EntityException: The underlying provider failed on Open`, inner
`Win32Exception: '%1 is not a valid Win32 application'`**
You are pointed at LocalDB instead of SQL Server Express. See step 5.

**Login page loads but has no styling**
`Styles.Render` failed. Confirm `src\ExpenseFlow.Web\Content\site.css` exists and
that `Microsoft.AspNet.Web.Optimization` restored.

**No live toast notifications**
jQuery and the SignalR 2 client come from a CDN — check the browser console. The
app is fully functional without them; only the toasts go away.

**"Cannot open database ExpenseFlow"**
Step 5 didn't run, or you're pointed at the wrong instance. Verify with the
`sqlcmd` query above.

**`HTTP Error 500.19` / web targets missing**
The **ASP.NET and web development** workload isn't installed. The web `.csproj`
imports `Microsoft.WebApplication.targets`, which that workload provides.
