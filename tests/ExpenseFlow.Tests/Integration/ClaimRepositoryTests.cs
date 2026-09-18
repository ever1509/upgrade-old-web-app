using System;
using System.Linq;
using ExpenseFlow.Data.Repositories;
using Xunit;

namespace ExpenseFlow.Tests.Integration
{
    /// <summary>
    /// The data layer as the web app uses it: a repository over a live
    /// DbContext. Runs on both frameworks, so when EF6 becomes EF Core (ledger
    /// B5) any change in how these queries behave shows up as one leg failing.
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public class ClaimRepositoryTests
    {
        private const string Alice = "alice@expenseflow.local";
        private const string Bob = "bob@expenseflow.local";
        private const string Dana = "dana@expenseflow.local";

        private readonly TestDatabase _db;

        public ClaimRepositoryTests(TestDatabase db)
        {
            _db = db;
        }

        [DatabaseFact]
        public void GetByIdWithDetails_loads_the_whole_graph_of_the_seeded_claim()
        {
            using (var ctx = _db.NewContext())
            {
                var id = ctx.Claims.Single(c => c.ClaimNumber == "CLM-000001").Id;

                var claim = new ClaimRepository(ctx).GetByIdWithDetails(id);

                Assert.Equal("Alice Moreno", claim.Employee.FullName);
                Assert.Equal("PRJ-APOLLO", claim.Project.Code);
                Assert.Equal(2, claim.Lines.Count);
                Assert.All(claim.Lines, l => Assert.NotNull(l.Category));
                Assert.Equal(31.15m, claim.Lines.Sum(l => l.Amount));
                Assert.Contains(claim.History, h => h.Action == "Created" && h.Actor != null);
            }
        }

        [DatabaseFact]
        public void GetByIdWithDetails_returns_null_for_an_unknown_claim()
        {
            using (var ctx = _db.NewContext())
                Assert.Null(new ClaimRepository(ctx).GetByIdWithDetails(-1));
        }

        [DatabaseFact]
        public void An_approver_only_sees_submitted_claims_from_their_own_reports()
        {
            var fromAlice = _db.InsertClaim(Alice, "Submitted", 40m);   // Alice reports to Bob
            var fromBob = _db.InsertClaim(Bob, "Submitted", 40m);       // Bob reports to Dana
            var draft = _db.InsertClaim(Alice, "Draft", 40m);

            using (var ctx = _db.NewContext())
            {
                var repo = new ClaimRepository(ctx);
                var bob = ctx.Employees.Single(e => e.Email == Bob);
                var dana = ctx.Employees.Single(e => e.Email == Dana);

                var forBob = repo.GetAwaitingDecisionFor(bob).Select(c => c.Id).ToList();
                var forDana = repo.GetAwaitingDecisionFor(dana).Select(c => c.Id).ToList();

                Assert.Contains(fromAlice.Id, forBob);
                Assert.DoesNotContain(fromBob.Id, forBob);      // never their own
                Assert.DoesNotContain(draft.Id, forBob);        // never a draft

                Assert.Contains(fromAlice.Id, forDana);         // an admin sees everyone
                Assert.Contains(fromBob.Id, forDana);
                Assert.DoesNotContain(draft.Id, forDana);
            }
        }

        [DatabaseFact]
        public void NextClaimNumber_is_one_past_the_most_recently_inserted_claim()
        {
            var latest = _db.InsertClaim(Alice, "Draft", 10m, claimNumber: "CLM-400000");

            using (var ctx = _db.NewContext())
                Assert.Equal("CLM-400001", new ClaimRepository(ctx).NextClaimNumber());
        }

        /// <summary>
        /// Characterises behaviour that is wrong, on purpose.
        ///
        /// NextClaimNumber reads the claim with the highest Id and parses the
        /// digits after "CLM-". If that claim's number is not in that format it
        /// silently restarts at CLM-000001 - which already exists, so the next
        /// insert would violate the unique constraint.
        ///
        /// Harmless in production today, since every claim is created through
        /// the same code path. Pinned here so the fix (a database sequence,
        /// ledger C5) is a visible, deliberate change of behaviour rather than
        /// an accident.
        /// </summary>
        [DatabaseFact]
        public void NextClaimNumber_restarts_at_one_when_the_latest_number_is_not_CLM_formatted()
        {
            _db.InsertClaim(Alice, "Draft", 10m, claimNumber: "IMPORTED-7");

            using (var ctx = _db.NewContext())
                Assert.Equal("CLM-000001", new ClaimRepository(ctx).NextClaimNumber());
        }
    }
}
