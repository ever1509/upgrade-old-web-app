# Phase 1 — get ExpenseFlow running in Windows 11 (Parallels)

Everything here happens **inside the Windows VM**. macOS gets involved at phase 4.

Budget ~45 minutes, most of it waiting on the Visual Studio installer.

---

## 1. Visual Studio 2026 Community

Install these two workloads:

- ✅ **ASP.NET and web development** — brings MSBuild web targets, IIS Express, Razor tooling
- ✅ **.NET desktop development** — needed for the Windows Service project

Under *Individual components*, confirm **.NET Framework 4.8 targeting pack** is
checked (the ASP.NET workload normally includes it).

> You do **not** need the old "ASP.NET Web Application (.NET Framework)" project
> template. Every `.csproj` in this repo is hand-written, so you just open the
> solution. If VS 2026 has dropped those templates, it changes nothing here.

## 2. Enable MSMQ

This is a Windows *feature*, not a NuGet package. Open **PowerShell as
Administrator**:

```powershell
Enable-WindowsOptionalFeature -Online -FeatureName MSMQ-Server -All
```

Reboot if it asks. Verify it took:

```powershell
Get-Service MSMQ
```

You want `Status: Running`. Without this the app still runs and claims still
submit — you'll just get a "notification service could not be reached" warning
and the worker will log a fatal error on startup.

## 3. Create the shared folders

The web app and the Windows Service exchange files through the filesystem, by
convention. Both configs point at these paths:

```powershell
New-Item -ItemType Directory -Force -Path C:\ExpenseFlow\uploads, C:\ExpenseFlow\pdf, C:\ExpenseFlow\mail
```

- `uploads` — receipt originals + generated thumbnails
- `pdf` — generated claim PDFs
- `mail` — outgoing email as `.eml` files (no SMTP server needed)

## 4. Clone the repo

```powershell
git clone <your-repo-url> C:\src\upgrade-old-web-app
```

Put it on the Windows filesystem, **not** on a Parallels shared folder pointing
at macOS. Building .NET Framework across the shared-folder bridge is slow and
occasionally produces file-locking errors.

## 5. Create the database

The app uses **LocalDB**, which ships with Visual Studio — nothing to install.

From the repo root in PowerShell:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -i db\01_schema.sql -i db\02_seed.sql -i db\03_reporting_procs.sql
```

If `sqlcmd` isn't on your PATH, open the three files in **SQL Server Object
Explorer** inside Visual Studio (View → SQL Server Object Explorer → connect to
`(localdb)\MSSQLLocalDB`) and run them in order.

Confirm it worked:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d ExpenseFlow -Q "SELECT FullName, Role FROM dbo.Employees"
```

You should see five people.

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
Step 2 didn't take. Check `Get-Service MSMQ`.

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
