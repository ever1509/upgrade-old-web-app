using System;
using System.Diagnostics;
using System.Web;
using ExpenseFlow.Data;
using ExpenseFlow.Domain.Entities;
using log4net;

namespace ExpenseFlow.Web.Modules
{
    /// <summary>
    /// Cross-cutting request logging as an IHttpModule, wired up in
    /// Web.config. Writes a row per request, synchronously, on the
    /// request thread - deliberately the naive implementation.
    ///
    /// Migration target: a middleware component registered in Program.cs.
    /// The shape is almost identical; what changes is that middleware is
    /// ordered explicitly in code instead of by config file position.
    /// </summary>
    public class AuditLogModule : IHttpModule
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AuditLogModule));
        private const string TimerKey = "ExpenseFlow.RequestTimer";

        public void Init(HttpApplication context)
        {
            context.BeginRequest += OnBeginRequest;
            context.EndRequest += OnEndRequest;
        }

        private void OnBeginRequest(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;
            app.Context.Items[TimerKey] = Stopwatch.StartNew();
        }

        private void OnEndRequest(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;
            var context = app.Context;

            var stopwatch = context.Items[TimerKey] as Stopwatch;
            if (stopwatch == null) return;
            stopwatch.Stop();

            var path = context.Request.Path ?? string.Empty;

            // Skip static assets so the table does not fill with noise.
            if (path.StartsWith("/Content", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/Scripts", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/bundles", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/signalr", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                using (var db = new ExpenseFlowContext())
                {
                    db.AuditLog.Add(new AuditLogEntry
                    {
                        OccurredUtc = DateTime.UtcNow,
                        UserName = context.User != null && context.User.Identity.IsAuthenticated
                                   ? context.User.Identity.Name
                                   : null,
                        HttpMethod = context.Request.HttpMethod,
                        Path = path.Length > 400 ? path.Substring(0, 400) : path,
                        StatusCode = context.Response.StatusCode,
                        DurationMs = (int)stopwatch.ElapsedMilliseconds
                    });
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                // Never let audit logging break the response.
                Log.Warn("Audit logging failed.", ex);
            }
        }

        public void Dispose() { }
    }
}
