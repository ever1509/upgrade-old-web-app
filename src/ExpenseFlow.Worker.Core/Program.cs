using System.Data.Entity;
using ExpenseFlow.Worker.Core;
using ExpenseFlow.Worker.Core.Data;
using ExpenseFlow.Worker.Core.Handlers;

// EF6 has no app.config here, so its SQL Server provider must be registered
// in code before any DbContext is constructed. See WorkerDbConfiguration.
DbConfiguration.SetConfiguration(new WorkerDbConfiguration());

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(
    builder.Configuration.GetSection(WorkerOptions.SectionName));

builder.Services.AddSingleton<IClaimStore, Ef6ClaimStore>();
builder.Services.AddSingleton<EmailSender>();

builder.Services.AddHttpClient<NotificationPusher>((provider, client) =>
{
    var options = provider.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<WorkerOptions>>().Value;
    client.BaseAddress = new Uri(options.WebBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHostedService<ClaimMessageWorker>();

var host = builder.Build();
await host.RunAsync();
