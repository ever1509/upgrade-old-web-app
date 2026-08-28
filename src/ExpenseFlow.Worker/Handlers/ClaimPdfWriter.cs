using System;
using System.IO;
using System.Linq;
using ExpenseFlow.Domain.Entities;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace ExpenseFlow.Worker.Handlers
{
    /// <summary>
    /// *** WINDOWS-ONLY API (indirectly) ***
    ///
    /// PdfSharp 1.50 draws through XGraphics, which is backed by GDI+ on
    /// .NET Framework. Same story as ThumbnailRenderer: it does not survive
    /// the move off Windows.
    ///
    /// Migration target: QuestPDF, or PdfSharpCore / PDFsharp 6 which use
    /// SkiaSharp instead of GDI+.
    /// </summary>
    public static class ClaimPdfWriter
    {
        public static string Write(string pdfRoot, ExpenseClaim claim)
        {
            if (!Directory.Exists(pdfRoot)) Directory.CreateDirectory(pdfRoot);

            var fileName = claim.ClaimNumber + ".pdf";
            var fullPath = Path.Combine(pdfRoot, fileName);

            using (var document = new PdfDocument())
            {
                document.Info.Title = "Expense claim " + claim.ClaimNumber;
                document.Info.Author = "ExpenseFlow";

                var page = document.AddPage();
                page.Size = PdfSharp.PageSize.A4;

                // Not a "using": on a page break the surface must be replaced,
                // so it is disposed explicitly in the finally below.
                var gfx = XGraphics.FromPdfPage(page);
                try
                {
                    var title = new XFont("Verdana", 16, XFontStyle.Bold);
                    var heading = new XFont("Verdana", 9, XFontStyle.Bold);
                    var body = new XFont("Verdana", 9, XFontStyle.Regular);

                    double y = 50;
                    const double left = 45;
                    double right = page.Width - 45;
                    double pageBottom = page.Height - 90;

                    gfx.DrawString("Expense claim " + claim.ClaimNumber, title, XBrushes.Black, new XPoint(left, y));
                    y += 26;

                    gfx.DrawString(claim.Title ?? string.Empty, body, XBrushes.Black, new XPoint(left, y));
                    y += 15;

                    var who = claim.Employee == null ? "(unknown)" : claim.Employee.FullName;
                    gfx.DrawString("Claimant: " + who, body, XBrushes.Black, new XPoint(left, y));
                    y += 15;

                    var project = claim.Project == null ? "(none)" : claim.Project.DisplayName;
                    gfx.DrawString("Project: " + project, body, XBrushes.Black, new XPoint(left, y));
                    y += 15;

                    var submitted = claim.SubmittedUtc.HasValue
                        ? claim.SubmittedUtc.Value.ToString("yyyy-MM-dd HH:mm") + " UTC"
                        : "(not submitted)";
                    gfx.DrawString("Submitted: " + submitted, body, XBrushes.Black, new XPoint(left, y));
                    y += 26;

                    gfx.DrawLine(XPens.Gray, left, y, right, y);
                    y += 14;

                    gfx.DrawString("Date", heading, XBrushes.Black, new XPoint(left, y));
                    gfx.DrawString("Category", heading, XBrushes.Black, new XPoint(left + 75, y));
                    gfx.DrawString("Description", heading, XBrushes.Black, new XPoint(left + 175, y));
                    gfx.DrawString("Amount", heading, XBrushes.Black,
                                   new XRect(right - 80, y - 10, 80, 14), XStringFormats.TopRight);
                    y += 6;
                    gfx.DrawLine(XPens.Gray, left, y, right, y);
                    y += 16;

                    foreach (var line in claim.Lines.OrderBy(l => l.ExpenseDate))
                    {
                        gfx.DrawString(line.ExpenseDate.ToString("yyyy-MM-dd"), body, XBrushes.Black, new XPoint(left, y));
                        gfx.DrawString(Truncate(line.Category == null ? "" : line.Category.Name, 16),
                                       body, XBrushes.Black, new XPoint(left + 75, y));
                        gfx.DrawString(Truncate(line.Description, 38), body, XBrushes.Black, new XPoint(left + 175, y));
                        gfx.DrawString(line.Amount.ToString("N2"), body, XBrushes.Black,
                                       new XRect(right - 80, y - 10, 80, 14), XStringFormats.TopRight);
                        y += 15;

                        if (y > pageBottom)
                        {
                            gfx.Dispose();
                            page = document.AddPage();
                            page.Size = PdfSharp.PageSize.A4;
                            gfx = XGraphics.FromPdfPage(page);
                            y = 50;
                        }
                    }

                    y += 6;
                    gfx.DrawLine(XPens.Gray, left, y, right, y);
                    y += 18;

                    gfx.DrawString("Total", heading, XBrushes.Black, new XPoint(left + 175, y));
                    gfx.DrawString(claim.TotalAmount.ToString("N2") + " USD", heading, XBrushes.Black,
                                   new XRect(right - 110, y - 10, 110, 14), XStringFormats.TopRight);

                    y += 34;
                    gfx.DrawString("Generated by the ExpenseFlow Windows Service on " +
                                   DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC",
                                   new XFont("Verdana", 7, XFontStyle.Italic), XBrushes.Gray, new XPoint(left, y));
                }
                finally
                {
                    gfx.Dispose();
                }

                document.Save(fullPath);
            }

            return fullPath;
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= max ? value : value.Substring(0, max - 1) + "\u2026";
        }
    }
}
