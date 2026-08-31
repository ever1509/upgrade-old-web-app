using System;
using System.Configuration;
using ExpenseFlow.Domain.Workflow;

namespace ExpenseFlow.Web
{
    /// <summary>
    /// Static configuration accessor reading ConfigurationManager directly.
    /// Untestable, un-injectable, and reachable from anywhere - which is
    /// exactly why it is everywhere in legacy code.
    ///
    /// Migration target: an ApprovalPolicy options class bound from
    /// appsettings.json and injected via IOptions&lt;T&gt;.
    /// </summary>
    public static class AppSettings
    {
        /// <summary>"msmq" or "file". Defaults to file, since current Windows cannot install MSMQ.</summary>
        public static string Transport
        {
            get { return Get("ExpenseFlow:Transport", "file"); }
        }

        public static string QueuePath
        {
            get { return Get("ExpenseFlow:QueuePath", @".\private$\expenseflow"); }
        }

        public static string QueueDirectory
        {
            get { return Get("ExpenseFlow:QueueDirectory", @"C:\ExpenseFlow\queue"); }
        }

        public static string UploadPath
        {
            get { return Get("ExpenseFlow:UploadPath", "~/App_Data/uploads"); }
        }

        public static string InternalApiKey
        {
            get { return Get("ExpenseFlow:InternalApiKey", "local-dev-worker-key"); }
        }

        public static ApprovalPolicy Policy
        {
            get
            {
                return new ApprovalPolicy
                {
                    SeniorApprovalThreshold = GetDecimal("ExpenseFlow:SeniorApprovalThreshold",
                                                         ApprovalPolicy.DefaultSeniorApprovalThreshold),
                    MaxClaimAmount = GetDecimal("ExpenseFlow:MaxClaimAmount",
                                                ApprovalPolicy.DefaultMaxClaimAmount),
                    MaxLinesPerClaim = GetInt("ExpenseFlow:MaxLinesPerClaim", 50)
                };
            }
        }

        private static string Get(string key, string fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static decimal GetDecimal(string key, decimal fallback)
        {
            decimal parsed;
            return decimal.TryParse(Get(key, null), out parsed) ? parsed : fallback;
        }

        private static int GetInt(string key, int fallback)
        {
            int parsed;
            return int.TryParse(Get(key, null), out parsed) ? parsed : fallback;
        }
    }
}
