namespace ExpenseFlow.Worker.Core;

/// <summary>
/// Strongly typed configuration, bound from appsettings.json.
///
/// Compare with the original worker's WorkerConfig: a static class reading
/// ConfigurationManager.AppSettings, reachable from anywhere and impossible to
/// substitute in a test. Ledger item C3, and nearly free once the generic host
/// is in place.
/// </summary>
public sealed class WorkerOptions
{
    public const string SectionName = "ExpenseFlow";

    /// <summary>Folder acting as the message queue, shared with the .NET Framework web app.</summary>
    public string QueueDirectory { get; set; } = "/tmp/expenseflow/queue";

    /// <summary>Where receipt originals and thumbnails live.</summary>
    public string UploadRoot { get; set; } = "/tmp/expenseflow/uploads";

    /// <summary>Where generated claim PDFs are written.</summary>
    public string PdfRoot { get; set; } = "/tmp/expenseflow/pdf";

    /// <summary>Where outgoing mail is written as .eml files. No SMTP server needed.</summary>
    public string MailPickupDirectory { get; set; } = "/tmp/expenseflow/mail";

    /// <summary>
    /// Passed explicitly to EF6 rather than resolved by name, because there is
    /// no app.config on this side of the migration.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Base URL of the .NET Framework web app, for the SignalR callback.</summary>
    public string WebBaseUrl { get; set; } = "http://localhost:52080/";

    /// <summary>Shared secret for the internal notify endpoint.</summary>
    public string InternalApiKey { get; set; } = "local-dev-worker-key";

    public string FromAddress { get; set; } = "expenseflow@localhost";

    /// <summary>How long to wait for a message before looping.</summary>
    public int ReceiveTimeoutSeconds { get; set; } = 2;
}
