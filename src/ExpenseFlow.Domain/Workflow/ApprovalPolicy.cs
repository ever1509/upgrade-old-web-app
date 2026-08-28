namespace ExpenseFlow.Domain.Workflow
{
    /// <summary>
    /// Policy knobs. In the legacy app these are read out of web.config
    /// AppSettings via a static accessor; after migration they become a
    /// strongly typed options class bound from appsettings.json.
    /// </summary>
    public class ApprovalPolicy
    {
        public const decimal DefaultSeniorApprovalThreshold = 500m;
        public const decimal DefaultMaxClaimAmount = 10000m;

        public ApprovalPolicy()
        {
            SeniorApprovalThreshold = DefaultSeniorApprovalThreshold;
            MaxClaimAmount = DefaultMaxClaimAmount;
            MaxLinesPerClaim = 50;
        }

        /// <summary>Claims at or above this amount must be decided by an Admin.</summary>
        public decimal SeniorApprovalThreshold { get; set; }

        /// <summary>Hard ceiling; a claim above this can never be submitted.</summary>
        public decimal MaxClaimAmount { get; set; }

        public int MaxLinesPerClaim { get; set; }
    }
}
