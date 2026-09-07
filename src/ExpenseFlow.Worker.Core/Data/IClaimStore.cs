using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Worker.Core.Data;

/// <summary>
/// Everything the worker needs from the database, and nothing else.
///
/// Deliberately an interface. MSMQ taught the lesson already: a dependency
/// behind a seam costs almost nothing to replace, while one reached for
/// directly costs a great deal. EF6 is ledger item B5 and the highest-risk
/// change in the whole migration, so it goes behind a seam BEFORE it is
/// swapped rather than after.
///
/// The surface is three methods. When EF Core arrives, that is the entire
/// contract that has to be satisfied.
/// </summary>
public interface IClaimStore
{
    /// <summary>Loads a claim with its lines, categories, receipts, employee and project.</summary>
    ExpenseClaim? GetClaimWithDetails(int claimId);

    /// <summary>Records the thumbnail path produced for a receipt.</summary>
    void SetReceiptThumbnail(int receiptId, string relativeThumbnailPath);

    /// <summary>Records the path of the generated claim PDF.</summary>
    void SetClaimPdfPath(int claimId, string pdfPath);
}
