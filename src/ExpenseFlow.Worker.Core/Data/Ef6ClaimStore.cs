using ExpenseFlow.Data;
using ExpenseFlow.Data.Repositories;
using ExpenseFlow.Domain.Entities;
using Microsoft.Extensions.Options;

namespace ExpenseFlow.Worker.Core.Data;

/// <summary>
/// EF6 implementation, running on .NET 10.
///
/// This is a transitional state, not a destination: EF6 works here only
/// because it ships a netstandard2.1 build, and it still drags in a
/// vulnerable System.Drawing.Common transitively (ledger C7). It stays until
/// B5 replaces it with EF Core, at which point only this one class changes.
///
/// Note the connection string is passed explicitly rather than read from a
/// config file. On .NET Framework the context resolved "name=ExpenseFlow"
/// through ConfigurationManager; there is no app.config here, so the value
/// comes from appsettings.json instead. A small but real migration detail.
/// </summary>
public sealed class Ef6ClaimStore : IClaimStore
{
    private readonly string _connectionString;

    public Ef6ClaimStore(IOptions<WorkerOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public ExpenseClaim? GetClaimWithDetails(int claimId)
    {
        using var db = new ExpenseFlowContext(_connectionString);

        // Return plain POCOs rather than change-tracked lazy-loading proxies.
        //
        // The obvious alternative - load the graph, then set the entry State to
        // Detached before the context is disposed - is WRONG, and silently so.
        // EF6 tears down the relationship manager when an entity is detached,
        // which clears its navigation properties: Employee, Project and Lines
        // all come back null or empty while the scalar columns survive. The
        // generated PDF then shows the right total over no line items, and
        // nothing anywhere reports an error.
        //
        // Turning both flags off means the Includes below are the only source
        // of related data, and the resulting objects are ordinary instances
        // that outlive the context perfectly well.
        db.Configuration.ProxyCreationEnabled = false;
        db.Configuration.LazyLoadingEnabled = false;

        return new ClaimRepository(db).GetByIdWithDetails(claimId);
    }

    public void SetReceiptThumbnail(int receiptId, string relativeThumbnailPath)
    {
        using var db = new ExpenseFlowContext(_connectionString);
        var receipt = db.Receipts.FirstOrDefault(r => r.Id == receiptId);
        if (receipt is null) return;

        receipt.ThumbnailPath = relativeThumbnailPath;
        db.SaveChanges();
    }

    public void SetClaimPdfPath(int claimId, string pdfPath)
    {
        using var db = new ExpenseFlowContext(_connectionString);
        var claim = db.Claims.FirstOrDefault(c => c.Id == claimId);
        if (claim is null) return;

        claim.PdfPath = pdfPath;
        db.SaveChanges();
    }
}
