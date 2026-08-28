using System.Linq;
using System.Web.Http;
using ExpenseFlow.Data;
using ExpenseFlow.Data.Repositories;
using ExpenseFlow.Web.Security;

namespace ExpenseFlow.Web.Controllers.Api
{
    /// <summary>
    /// Web API 2 controller. Note it inherits ApiController (not Controller),
    /// uses IHttpActionResult (not ActionResult), and is configured by a
    /// completely separate HttpConfiguration. Two parallel stacks in one app.
    ///
    /// In ASP.NET Core both collapse into a single ControllerBase.
    /// </summary>
    [RoutePrefix("api/claims")]
    public class ClaimsApiController : ApiController
    {
        [HttpGet]
        [Route("mine")]
        public IHttpActionResult Mine()
        {
            var me = CurrentUser.Employee;
            if (me == null) return Unauthorized();

            using (var db = new ExpenseFlowContext())
            {
                var repo = new ClaimRepository(db);
                var claims = repo.GetForEmployee(me.Id)
                    .Select(c => new
                    {
                        c.Id,
                        c.ClaimNumber,
                        c.Title,
                        c.Status,
                        c.TotalAmount,
                        c.SubmittedUtc,
                        Project = c.Project == null ? null : c.Project.Code
                    })
                    .ToList();

                return Ok(claims);
            }
        }

        [HttpGet]
        [Route("summary")]
        public IHttpActionResult Summary()
        {
            var me = CurrentUser.Employee;
            if (me == null) return Unauthorized();

            using (var db = new ExpenseFlowContext())
            {
                var repo = new ClaimRepository(db);
                var claims = repo.GetForEmployee(me.Id);

                return Ok(new
                {
                    total = claims.Count,
                    draft = claims.Count(c => c.Status == "Draft"),
                    submitted = claims.Count(c => c.Status == "Submitted"),
                    approved = claims.Count(c => c.Status == "Approved"),
                    rejected = claims.Count(c => c.Status == "Rejected"),
                    approvedAmount = claims.Where(c => c.Status == "Approved" || c.Status == "Reimbursed")
                                           .Sum(c => (decimal?)c.TotalAmount) ?? 0m
                });
            }
        }
    }
}
