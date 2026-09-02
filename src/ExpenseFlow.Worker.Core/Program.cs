using ExpenseFlow.Worker.Core;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(
    builder.Configuration.GetSection(WorkerOptions.SectionName));

builder.Services.AddHostedService<ClaimMessageWorker>();

var host = builder.Build();
await host.RunAsync();
