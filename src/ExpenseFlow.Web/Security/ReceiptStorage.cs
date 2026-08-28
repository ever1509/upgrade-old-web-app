using System;
using System.IO;
using System.Web;

namespace ExpenseFlow.Web.Security
{
    /// <summary>
    /// Receipt files on the local filesystem, resolved with Server.MapPath.
    /// Ties the app to one machine's disk layout and to System.Web.
    ///
    /// Migration target: IWebHostEnvironment.ContentRootPath behind an
    /// IFileStore abstraction - which then makes blob storage a drop-in.
    /// </summary>
    public static class ReceiptStorage
    {
        public static string RootPath
        {
            get
            {
                var configured = AppSettings.UploadPath;

                // A "~/..." path is resolved through Server.MapPath - the
                // System.Web-only API. An absolute path is used as-is, which
                // is what lets the Windows Service see the same files.
                var physical = configured.StartsWith("~")
                    ? HttpContext.Current.Server.MapPath(configured)
                    : configured;

                if (!Directory.Exists(physical)) Directory.CreateDirectory(physical);
                return physical;
            }
        }

        /// <summary>Saves the upload and returns its path relative to the upload root.</summary>
        public static string Save(HttpPostedFileBase file, int claimId)
        {
            var folder = Path.Combine(RootPath, claimId.ToString());
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var safeName = Path.GetFileName(file.FileName);
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8) + "_" + safeName;
            var fullPath = Path.Combine(folder, unique);

            file.SaveAs(fullPath);

            return Path.Combine(claimId.ToString(), unique);
        }

        public static string ToPhysicalPath(string relativePath)
        {
            return Path.Combine(RootPath, relativePath);
        }

        public static bool IsAllowed(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0) return false;
            if (file.ContentLength > 10 * 1024 * 1024) return false;

            var ext = (Path.GetExtension(file.FileName) ?? string.Empty).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".pdf";
        }
    }
}
