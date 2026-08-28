using System;

namespace ExpenseFlow.Domain.Entities
{
    public class Receipt
    {
        public Receipt()
        {
            UploadedUtc = DateTime.UtcNow;
        }

        public int Id { get; set; }
        public int ExpenseLineId { get; set; }
        public string FileName { get; set; }
        public string StoredPath { get; set; }
        public string ThumbnailPath { get; set; }
        public string ContentType { get; set; }
        public long SizeBytes { get; set; }
        public DateTime UploadedUtc { get; set; }

        public virtual ExpenseLine ExpenseLine { get; set; }

        public bool HasThumbnail
        {
            get { return !string.IsNullOrEmpty(ThumbnailPath); }
        }
    }
}
