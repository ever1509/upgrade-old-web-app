namespace ExpenseFlow.Worker.Core;

/// <summary>
/// Strongly typed configuration, bound from appsettings.json.
///
/// Compare with the original worker's WorkerConfig: a static class reading
/// ConfigurationManager.AppSettings, untestable and reachable from anywhere.
/// This is ledger item C3, and it is nearly free once the host is in place.
/// </summary>
public sealed class WorkerOptions
{
    public const string SectionName = "ExpenseFlow";

    /// <summary>Folder acting as the message queue. Shared with the .NET Framework web app.</summary>
    public string QueueDirectory { get; set; } = "/tmp/expenseflow/queue";

    /// <summary>Where receipt originals and thumbnails live.</summary>
    public string UploadRoot { get; set; } = "/tmp/expenseflow/uploads";

    /// <summary>Where generated claim PDFs are written.</summary>
    public string PdfRoot { get; set; } = "/tmp/expenseflow/pdf";

    /// <summary>How long to wait for a message before looping.</summary>
    public int ReceiveTimeoutSeconds { get; set; } = 2;
}
