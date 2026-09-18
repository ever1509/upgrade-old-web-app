using System.Runtime.InteropServices;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Direct forwarding: the lightweight part of YARP, used by Microsoft's
// incremental migration template. No route/cluster configuration needed for a
// single catch-all destination.
builder.Services.AddHttpForwarder();

var app = builder.Build();

var legacyUrl = (app.Configuration["ProxyTo"]
    ?? throw new InvalidOperationException("ProxyTo is not configured."))
    .TrimEnd('/');

// Stamp every response with the app that produced it, so the strangler is
// visible in the browser's dev tools: open the Network tab and look for
// X-ExpenseFlow-Served-By. "core" is this app; "legacy" is the .NET Framework
// app answering through the forwarder. Migration progress, one header at a time.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.TryAdd("X-ExpenseFlow-Served-By", "core");
        return Task.CompletedTask;
    });
    await next();
});

// ---- Routes served by this app ----------------------------------------
// Empty for now apart from a status page. Slice 2b moves /Admin/Reports here.

app.MapGet("/_core/status", () => Results.Ok(new
{
    app = "ExpenseFlow.Web.Core",
    framework = RuntimeInformation.FrameworkDescription,
    os = RuntimeInformation.OSDescription,
    proxyTo = legacyUrl
}));

// ---- Everything else goes to the .NET Framework app --------------------
// Order = int.MaxValue makes this the lowest-priority endpoint, so any route
// this app defines always wins, and a page moves across simply by being
// implemented here.
app.MapForwarder("/{**catch-all}", legacyUrl, transforms =>
{
    transforms.AddResponseHeader("X-ExpenseFlow-Served-By", "legacy",
        append: false, ResponseCondition.Always);

    // If the old app ever emits an absolute redirect to its own address, the
    // browser would follow it straight past the proxy and the user would
    // silently end up talking to the old app directly. Rewrite such redirects
    // back to relative paths so every navigation stays on the front door.
    transforms.AddResponseTransform(context =>
    {
        if (context.ProxyResponse is null) return ValueTask.CompletedTask;

        var location = context.HttpContext.Response.Headers.Location.ToString();
        if (location.StartsWith(legacyUrl, StringComparison.OrdinalIgnoreCase))
        {
            var relative = location[legacyUrl.Length..];
            context.HttpContext.Response.Headers.Location = relative.Length > 0 ? relative : "/";
        }
        return ValueTask.CompletedTask;
    });
})
.Add(endpoint => ((RouteEndpointBuilder)endpoint).Order = int.MaxValue);

app.Run();
