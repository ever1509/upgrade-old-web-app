#if !NET48
using System.Linq;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Worker.Core;
using ExpenseFlow.Worker.Core.Data;
using Microsoft.Extensions.Options;
using Xunit;

namespace ExpenseFlow.Tests.Integration
{
    /// <summary>
    /// The .NET 10 worker's data access. .NET 10 only, because the worker is.
    ///
    /// The first test is the regression for the bug found in slice 1: the
    /// store returned a claim whose navigation properties had been emptied by
    /// an EF6 Detach, so the generated PDF had the right total and no lines.
    /// No existing test could have caught it. This one would have.
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public class ClaimStoreTests
    {
        private readonly TestDatabase _db;

        public ClaimStoreTests(TestDatabase db)
        {
            _db = db;
        }

        private Ef6ClaimStore Store()
        {
            return new Ef6ClaimStore(Options.Create(new WorkerOptions { ConnectionString = _db.ConnectionString }));
        }

        [DatabaseFact]
        public void GetClaimWithDetails_returns_the_full_graph_after_its_context_is_disposed()
        {
            var inserted = _db.InsertClaim("alice@expenseflow.local", "Submitted", 60m, withReceipt: true);

            var claim = Store().GetClaimWithDetails(inserted.Id);

            Assert.NotNull(claim);
            Assert.Equal("Alice Moreno", claim.Employee?.FullName);
            Assert.Equal("PRJ-APOLLO", claim.Project?.Code);

            var line = Assert.Single(claim.Lines);
            Assert.Equal("Meals", line.Category?.Name);
            Assert.Equal(60m, line.Amount);

            var receipt = Assert.Single(line.Receipts);
            Assert.Equal("it/receipt.jpg", receipt.StoredPath);
        }

        [DatabaseFact]
        public void GetClaimWithDetails_returns_plain_objects_rather_than_lazy_loading_proxies()
        {
            var inserted = _db.InsertClaim("alice@expenseflow.local", "Submitted", 20m);

            var claim = Store().GetClaimWithDetails(inserted.Id);

            // A proxy would be a generated subclass. Plain instances are what
            // make the graph safe to use after the context has gone.
            Assert.Equal(typeof(ExpenseClaim), claim.GetType());
        }

        [DatabaseFact]
        public void GetClaimWithDetails_returns_null_for_an_unknown_claim()
        {
            Assert.Null(Store().GetClaimWithDetails(-1));
        }

        [DatabaseFact]
        public void SetClaimPdfPath_writes_the_path_back()
        {
            var inserted = _db.InsertClaim("alice@expenseflow.local", "Submitted", 20m);

            Store().SetClaimPdfPath(inserted.Id, @"C:\ExpenseFlow\pdf\test.pdf");

            using (var ctx = _db.NewContext())
                Assert.Equal(@"C:\ExpenseFlow\pdf\test.pdf",
                             ctx.Claims.Find(inserted.Id).PdfPath);
        }

        [DatabaseFact]
        public void SetReceiptThumbnail_writes_the_path_back()
        {
            var inserted = _db.InsertClaim("alice@expenseflow.local", "Submitted", 60m, withReceipt: true);
            var receiptId = inserted.Lines.Single().Receipts.Single().Id;

            Store().SetReceiptThumbnail(receiptId, "it/receipt_thumb.jpg");

            using (var ctx = _db.NewContext())
                Assert.Equal("it/receipt_thumb.jpg", ctx.Receipts.Find(receiptId).ThumbnailPath);
        }
    }
}
#endif
