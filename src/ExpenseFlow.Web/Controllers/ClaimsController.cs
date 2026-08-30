using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Workflow;
using ExpenseFlow.Messaging.Contracts;
using ExpenseFlow.Web.Models;
using ExpenseFlow.Web.Security;
using log4net;

namespace ExpenseFlow.Web.Controllers
{
    public class ClaimsController : BaseController
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ClaimsController));

        public ActionResult Index()
        {
            var me = Me;
            if (me == null) return RedirectToAction("Login", "Account");

            return View(new ClaimListViewModel
            {
                Claims = Claims.GetForEmployee(me.Id),
                Heading = "My claims",
                ShowClaimant = false
            });
        }

        [HttpGet]
        public ActionResult Create()
        {
            return View(new CreateClaimViewModel { Projects = Lookups.ActiveProjects() });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateClaimViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Projects = Lookups.ActiveProjects();
                return View(model);
            }

            var me = Me;
            var claim = new ExpenseClaim
            {
                ClaimNumber = Claims.NextClaimNumber(),
                EmployeeId = me.Id,
                ProjectId = model.ProjectId,
                Title = model.Title,
                CreatedUtc = DateTime.UtcNow
            };
            claim.StatusValue = Domain.Enums.ClaimStatus.Draft;

            claim.History.Add(new ApprovalHistory
            {
                Action = Domain.Enums.ApprovalAction.Created.ToString(),
                ActorEmployeeId = me.Id,
                OccurredUtc = DateTime.UtcNow
            });

            Claims.Add(claim);
            Claims.Save();

            Log.InfoFormat("{0} created claim {1}", me.Email, claim.ClaimNumber);
            return RedirectToAction("Details", new { id = claim.Id });
        }

        [HttpGet]
        public ActionResult Details(int id)
        {
            var claim = Claims.GetByIdWithDetails(id);
            var me = Me;

            var canView = ClaimWorkflow.CanView(claim, me);
            if (!canView.IsAllowed) return Denied(canView.FirstError);

            var policy = AppSettings.Policy;
            var submitCheck = ClaimWorkflow.CanSubmit(claim, policy);
            var decideCheck = ClaimWorkflow.CanDecide(claim, me, policy);
            var reimburseCheck = ClaimWorkflow.CanReimburse(claim, me);

            return View(new ClaimDetailsViewModel
            {
                Claim = claim,
                Categories = Lookups.ActiveCategories(),
                CanEdit = ClaimWorkflow.CanEdit(claim, me).IsAllowed,
                CanSubmit = submitCheck.IsAllowed,
                CanDecide = decideCheck.IsAllowed,
                CanReimburse = reimburseCheck.IsAllowed,
                BlockingReasons = submitCheck.Errors.ToList(),
                NewLine = new AddLineViewModel { ClaimId = id }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddLine(AddLineViewModel model)
        {
            var claim = Claims.GetByIdWithDetails(model.ClaimId);
            var me = Me;

            var canEdit = ClaimWorkflow.CanEdit(claim, me);
            if (!canEdit.IsAllowed) return Denied(canEdit.FirstError);

            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction("Details", new { id = model.ClaimId });
            }

            var line = new ExpenseLine
            {
                ClaimId = claim.Id,
                CategoryId = model.CategoryId,
                ExpenseDate = model.ExpenseDate,
                Description = model.Description,
                Amount = model.Amount,
                Currency = "USD"
            };

            Claims.AddLine(line);
            Claims.Save();

            claim.TotalAmount = Claims.GetByIdWithDetails(claim.Id).CalculateTotal();
            Claims.Save();

            return RedirectToAction("Details", new { id = model.ClaimId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveLine(int lineId)
        {
            var line = Claims.GetLine(lineId);
            if (line == null) return Denied("Line not found.");

            var claim = Claims.GetByIdWithDetails(line.ClaimId);
            var canEdit = ClaimWorkflow.CanEdit(claim, Me);
            if (!canEdit.IsAllowed) return Denied(canEdit.FirstError);

            Claims.RemoveLine(line);
            Claims.Save();

            var refreshed = Claims.GetByIdWithDetails(claim.Id);
            refreshed.TotalAmount = refreshed.CalculateTotal();
            Claims.Save();

            return RedirectToAction("Details", new { id = claim.Id });
        }

        /// <summary>
        /// Receipt upload. Saves the original to disk immediately; the
        /// thumbnail is produced later by the Windows Service, off the
        /// request thread, once the claim is submitted.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadReceipt(int lineId, HttpPostedFileBase file)
        {
            var line = Claims.GetLine(lineId);
            if (line == null) return Denied("Line not found.");

            var claim = Claims.GetByIdWithDetails(line.ClaimId);
            var canEdit = ClaimWorkflow.CanEdit(claim, Me);
            if (!canEdit.IsAllowed) return Denied(canEdit.FirstError);

            if (!ReceiptStorage.IsAllowed(file))
            {
                TempData["Error"] = "Receipts must be a jpg, png, gif or pdf under 10 MB.";
                return RedirectToAction("Details", new { id = claim.Id });
            }

            var relativePath = ReceiptStorage.Save(file, claim.Id);

            // Fully qualified: this class declares a Receipt() action, and a
            // member name hides a type of the same name.
            Claims.AddReceipt(new ExpenseFlow.Domain.Entities.Receipt
            {
                ExpenseLineId = line.Id,
                FileName = Path.GetFileName(file.FileName),
                StoredPath = relativePath,
                ContentType = file.ContentType,
                SizeBytes = file.ContentLength,
                UploadedUtc = DateTime.UtcNow
            });
            Claims.Save();

            return RedirectToAction("Details", new { id = claim.Id });
        }

        public ActionResult Receipt(int id)
        {
            var receipt = Claims.GetReceipt(id);
            if (receipt == null) return HttpNotFound();

            var claim = Claims.GetByIdWithDetails(Claims.GetLine(receipt.ExpenseLineId).ClaimId);
            var canView = ClaimWorkflow.CanView(claim, Me);
            if (!canView.IsAllowed) return new HttpStatusCodeResult(403);

            var physical = ReceiptStorage.ToPhysicalPath(receipt.StoredPath);
            if (!System.IO.File.Exists(physical)) return HttpNotFound();

            return File(physical, receipt.ContentType);
        }

        /// <summary>
        /// Submit. Writes the state change, then publishes to MSMQ.
        ///
        /// Note the dual write: SaveChanges and Publish are two separate
        /// transactions. If the process dies between them the claim is
        /// submitted but no one is ever told. That is the exact problem the
        /// transactional outbox pattern solves - a phase 5 exercise.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Submit(int id)
        {
            var claim = Claims.GetByIdWithDetails(id);
            var me = Me;

            var canEdit = ClaimWorkflow.CanEdit(claim, me);
            if (!canEdit.IsAllowed) return Denied(canEdit.FirstError);

            var check = ClaimWorkflow.CanSubmit(claim, AppSettings.Policy);
            if (!check.IsAllowed)
            {
                TempData["Error"] = string.Join(" ", check.Errors);
                return RedirectToAction("Details", new { id = id });
            }

            ClaimWorkflow.Submit(claim, me);
            Claims.Save();

            var approver = claim.Employee != null && claim.Employee.ManagerId.HasValue
                           ? Employees.GetById(claim.Employee.ManagerId.Value)
                           : null;

            try
            {
                Publisher.Publish(ClaimSubmittedMessage.Type, new ClaimSubmittedMessage
                {
                    ClaimId = claim.Id,
                    ClaimNumber = claim.ClaimNumber,
                    EmployeeId = claim.EmployeeId,
                    EmployeeName = me.FullName,
                    EmployeeEmail = me.Email,
                    ApproverEmployeeId = approver == null ? (int?)null : approver.Id,
                    ApproverEmail = approver == null ? null : approver.Email,
                    TotalAmount = claim.TotalAmount,
                    Title = claim.Title
                }, claim.ClaimNumber);
            }
            catch (Exception ex)
            {
                // The claim IS submitted. Only the notification failed.
                Log.Error("Could not publish claim.submitted. Is the MSMQ feature installed?", ex);
                TempData["Warning"] = "Claim submitted, but the notification service could not be reached.";
            }

            TempData["Success"] = "Claim " + claim.ClaimNumber + " submitted.";
            return RedirectToAction("Details", new { id = id });
        }
    }
}
