# Phase 1 verification — prove the app really works

Do this **before** touching anything migration-related. You cannot tell whether a
migration broke something if you never established what "working" looks like.

Work through it in order and tick each box. Where a step says *watch the worker*,
look at the worker's console window.

---

## A. Authentication

- [ ] `http://localhost:52080/` redirects an anonymous visitor to `/Account/Login`
- [ ] Signing in with a wrong password shows *"That email and password combination was not recognised."* — and does **not** reveal whether the email exists
- [ ] Sign in as `alice@expenseflow.local` / `Passw0rd!` → lands on the dashboard
- [ ] The nav bar shows **Dashboard** and **My claims**, but *not* Approvals or Reports
- [ ] Sign out returns you to the login page and `/Claims` is no longer reachable

> What you just verified: Forms Authentication, the `Global.asax`
> `PostAuthenticateRequest` role rebuild, and `<deny users="?" />` in `web.config`.

## B. The seeded draft claim

- [ ] **My claims** lists `CLM-000001` — "Client workshop - Berlin", status **Draft**, total **31.15**
- [ ] Opening it shows two lines (Meals 18.40, Taxi 12.75) and a History entry "Created"

## C. Business rules — the important part

Still as Alice, on `CLM-000001`:

- [ ] It can be submitted as-is (both lines are under their receipt thresholds)
- [ ] Add a line: category **Meals**, amount **60.00** → the Actions panel now blocks submission with *"…needs a receipt: Meals allows at most 25.00 without one"*
- [ ] Attach any jpg/png to that line → the blocker disappears and **Submit for approval** returns
- [ ] Add a line with a **future date** → blocked with *"Expense lines cannot be dated in the future"*
- [ ] Remove that line → submission is possible again

> What you just verified: `ClaimWorkflow.CanSubmit`. **These are exactly the
> assertions your phase 2 characterization tests will encode.** Write down the
> messages you see — they're the expected values.

## D. The queue, the worker, the notification pipeline

This is the flow the whole exercise is built around — and on a machine without
MSMQ you cannot run it yet. Skip to section E and come back once the transport
has been replaced with RabbitMQ. Nothing else in this checklist depends on it.

- [ ] Click **Submit for approval**
- [ ] The page shows *"Claim CLM-000001 submitted."* and status flips to **Submitted**
- [ ] *Watch the worker*: `Received claim.submitted`
- [ ] *Watch the worker*: `Thumbnail rendered for receipt N` — **System.Drawing/GDI+**
- [ ] *Watch the worker*: `PDF written: C:\ExpenseFlow\pdf\CLM-000001.pdf` — **PdfSharp/GDI+**
- [ ] *Watch the worker*: `Email queued for bob@expenseflow.local` — **SmtpClient**
- [ ] `C:\ExpenseFlow\pdf\CLM-000001.pdf` exists and opens, listing every line and the total
- [ ] `C:\ExpenseFlow\mail\` contains a new `.eml` — open it; the PDF is attached
- [ ] `C:\ExpenseFlow\uploads\1\` contains both the original receipt and a `_thumb.jpg`
- [ ] A toast appears bottom-right in the browser: *"Claim CLM-000001 submitted"* — **SignalR 2**

> If the toast doesn't appear but everything else does, the CDN is blocked.
> Not a problem — note it and move on.

**Now inspect the queue itself.** Open `compmgmt.msc` → Services and Applications
→ Message Queuing → Private Queues → `expenseflow`. It should be empty (drained).
Stop the worker, submit another claim, and watch a message *sit* in that queue
until you restart the worker.

- [ ] Messages visibly queue up while the worker is stopped, and drain when it starts

> That's the strangler seam you'll exploit in phase 5: producer and consumer are
> already decoupled processes.

## E. Approval rules

- [ ] Sign out; sign in as `bob@expenseflow.local` / `Passw0rd!`
- [ ] Bob's nav bar now includes **Approvals**
- [ ] The dashboard warns *"1 claim(s) are waiting for your decision"*
- [ ] **Approvals** lists Alice's claim
- [ ] Open it → an approve/reject panel appears with a comment box
- [ ] **Reject** with no comment → *"A rejection needs a reason."*
- [ ] **Reject** with a reason → status becomes **Rejected**, reason shown in red
- [ ] *Watch the worker*: `Received claim.decided`, another `.eml` written
- [ ] Sign back in as Alice → the claim is editable again, History shows the rejection
- [ ] Resubmit it → History records **Resubmitted**, not a second "Submitted"

### The two rules worth verifying explicitly

- [ ] **You cannot decide your own claim.** As Bob, create a claim, add a line, submit it. Bob does *not* get an approve panel on his own claim.
- [ ] **Claims ≥ 500 need an Admin.** As Alice, create a claim with a line of **750.00** (attach a receipt) and submit. As Bob → *"Claims of 500.00 or more must be decided by Finance (Admin role)."* Sign in as `dana@expenseflow.local` → Dana **can** decide it.

- [ ] As Dana, approve a claim, then **Mark reimbursed** → status **Reimbursed**
- [ ] As Bob, the *Mark reimbursed* button never appears (Admin only)

## F. Web API 2 — the parallel stack

Signed in, in the same browser (these ride the same Forms cookie):

- [ ] <http://localhost:52080/api/claims/summary> returns JSON counts
- [ ] <http://localhost:52080/api/claims/mine> returns your claims as JSON
- [ ] Property names are **camelCase** (configured in `WebApiConfig`)
- [ ] In a private window (signed out), both **302-redirect to the login page** rather than returning 401 — Forms Auth treats API routes exactly like pages. Note it: it's a genuine legacy wart the migration fixes.

> What you just verified: a Web API 2 stack with its own routing, its own
> configuration, and its own base class, living beside MVC 5 in one app. Collapsing
> these two is a headline win of the migration.

## G. Reporting (stored procedures)

- [ ] As Dana → **Reports**
- [ ] Both tables populate with the claims you approved (`usp_SpendByDepartment`, `usp_SpendByCategory`)
- [ ] Narrowing the date range changes the numbers

## H. The audit HttpModule

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d ExpenseFlow -Q "SELECT TOP 20 OccurredUtc, UserName, HttpMethod, Path, StatusCode, DurationMs FROM dbo.AuditLog ORDER BY Id DESC"
```

- [ ] Rows exist for the pages you visited, with your email and a duration
- [ ] `/Content`, `/Scripts` and `/signalr` are **absent** (filtered out)

> One synchronous INSERT per request, on the request thread. Look at `DurationMs`
> and note that the audit write is itself part of the latency. Record it — it's a
> performance finding for the assessment.

## I. Logs

- [ ] `src\ExpenseFlow.Web\App_Data\logs\expenseflow.log` has sign-in and claim entries
- [ ] `src\ExpenseFlow.Worker\bin\Debug\logs\worker.log` mirrors the console

---

## Capture your baseline

Before moving on, write down:

1. **The exact validation messages** from section C — future assertions.
2. **A screenshot** of a claim's Details page in each of the five statuses.
3. **A saved copy** of one generated PDF and one `.eml` — you will diff the
   migrated output against these.
4. **A typical `DurationMs`** from the audit table — your performance baseline.

That's your golden master. Phase 2 turns it into executable tests.

---

## Phase 1 is done when

You can submit a claim end to end, watch five different Windows-only
dependencies fire in the worker log, and approve it as a different user with the
role rules enforced correctly.

At that point you have something real to migrate — and, more importantly, you
know exactly what "working" means.
