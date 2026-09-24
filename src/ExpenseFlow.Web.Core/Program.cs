using System.Runtime.InteropServices;
using System.Security.Claims;
using Microsoft.AspNetCore.SystemWebAdapters;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Direct forwarding: the lightweight part of YARP, used by Microsoft's
// incremental migration template. No route/cluster configuration needed for a
// single catch-all destination.
builder.Services.AddHttpForwarder();

// Remote authentication.
//
// The Forms Authentication cookie is encrypted with the legacy app's machine
// key, so this app cannot read it. Instead the System.Web adapters call the
// legacy app - which can - and turn its answer into a ClaimsPrincipal here.
// That means a page can move across without auth moving with it, which is why
// authentication is scheduled last (ledger B9) rather than first.
builder.Services
    .AddSystemWebAdapters()
    .AddRemoteAppClient(options =>
    {
        options.RemoteAppUrl = new Uri(builder.Configuration["ProxyTo"]
            ?? throw new InvalidOperationException("ProxyTo is not configured."));
        options.ApiKey = builder.Configuration["RemoteAppApiKey"]
            ?? throw new InvalidOperationException("RemoteAppApiKey is not configured.");
    })
    .AddAuthenticationClient(isDefaultScheme: true);

builder.Services.AddAuthorization();

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

// Served before authentication on purpose.
//
// UseAuthentication resolves the user on every request, and doing that means
// calling the legacy app. So once remote authentication is on, ANY request
// through this app fails while the legacy app is down - including diagnostics.
// Short-circuiting here keeps the status endpoint answering when the thing you
// are trying to diagnose is the legacy app itself.
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/_core/status")
    {
        await Results.Ok(new
        {
            app = "ExpenseFlow.Web.Core",
            framework = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            proxyTo = legacyUrl
        }).ExecuteAsync(context);
        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// Makes the remotely-resolved user available to this app's endpoints.
app.UseSystemWebAdapters();

// ---- Routes served by this app ----------------------------------------
// Empty for now apart from a status page. Slice 2b moves /Admin/Reports here.

// Proves remote authentication end to end: sign in through the legacy app,
// then load this and see the same user, resolved by a .NET 10 process that
// cannot decrypt the cookie itself.
app.MapGet("/_core/whoami", (ClaimsPrincipal user) => Results.Ok(new
{
    isAuthenticated = user.Identity?.IsAuthenticated ?? false,
    name = user.Identity?.Name,
    authenticationType = user.Identity?.AuthenticationType,
    isAdmin = user.IsInRole("Admin"),
    isApprover = user.IsInRole("Approver"),
    roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
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
