using System;

namespace ExpenseFlow.Domain.Entities
{
    /// <summary>Written by the legacy IHttpModule on every request.</summary>
    public class AuditLogEntry
    {
        public long Id { get; set; }
        public DateTime OccurredUtc { get; set; }
        public string UserName { get; set; }
        public string HttpMethod { get; set; }
        public string Path { get; set; }
        public int StatusCode { get; set; }
        public int DurationMs { get; set; }
    }
}
