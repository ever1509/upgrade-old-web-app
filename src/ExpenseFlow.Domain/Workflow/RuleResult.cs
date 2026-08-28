using System.Collections.Generic;
using System.Linq;

namespace ExpenseFlow.Domain.Workflow
{
    public class RuleResult
    {
        private readonly List<string> _errors = new List<string>();

        public bool IsAllowed { get { return !_errors.Any(); } }
        public IEnumerable<string> Errors { get { return _errors; } }
        public string FirstError { get { return _errors.FirstOrDefault(); } }

        public static RuleResult Allow()
        {
            return new RuleResult();
        }

        public static RuleResult Deny(string reason)
        {
            var r = new RuleResult();
            r._errors.Add(reason);
            return r;
        }

        public RuleResult And(bool condition, string reasonIfFalse)
        {
            if (!condition) _errors.Add(reasonIfFalse);
            return this;
        }

        public RuleResult Merge(RuleResult other)
        {
            if (other != null) _errors.AddRange(other._errors);
            return this;
        }

        public override string ToString()
        {
            return IsAllowed ? "Allowed" : string.Join("; ", _errors);
        }
    }
}
