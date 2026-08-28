using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace ExpenseFlow.Worker.Handlers
{
    /// <summary>
    /// *** WINDOWS-ONLY API ***
    ///
    /// System.Drawing.Common is a thin wrapper over GDI+. Since .NET 6 it
    /// throws PlatformNotSupportedException on anything that is not Windows,
    /// and Microsoft's guidance is to move to ImageSharp, SkiaSharp or
    /// Microsoft.Maui.Graphics.
    ///
    /// This class is why the worker cannot run on macOS. Migration target:
    /// SixLabors.ImageSharp - the API shape is close enough that this is a
    /// contained, mechanical rewrite.
    /// </summary>
    public static class ThumbnailRenderer
    {
        public const int MaxEdge = 320;

        public static bool IsRenderable(string contentType, string fileName)
        {
            if (!string.IsNullOrEmpty(contentType) &&
                contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return true;

            var ext = (Path.GetExtension(fileName) ?? string.Empty).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif";
        }

        /// <summary>Writes a thumbnail beside the original and returns its relative path.</summary>
        public static string Render(string uploadRoot, string relativeSourcePath)
        {
            var source = Path.Combine(uploadRoot, relativeSourcePath);
            if (!File.Exists(source)) throw new FileNotFoundException("Receipt file is missing.", source);

            var directory = Path.GetDirectoryName(relativeSourcePath) ?? string.Empty;
            var thumbName = Path.GetFileNameWithoutExtension(relativeSourcePath) + "_thumb.jpg";
            var relativeTarget = string.IsNullOrEmpty(directory) ? thumbName : Path.Combine(directory, thumbName);
            var target = Path.Combine(uploadRoot, relativeTarget);

            using (var original = Image.FromFile(source))
            {
                var scale = Math.Min((double)MaxEdge / original.Width, (double)MaxEdge / original.Height);
                if (scale > 1) scale = 1;

                var width = Math.Max(1, (int)(original.Width * scale));
                var height = Math.Max(1, (int)(original.Height * scale));

                using (var thumb = new Bitmap(width, height))
                using (var g = Graphics.FromImage(thumb))
                {
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.White);
                    g.DrawImage(original, 0, 0, width, height);

                    SaveJpeg(thumb, target, 82L);
                }
            }

            return relativeTarget;
        }

        private static void SaveJpeg(Image image, string path, long quality)
        {
            var encoder = GetEncoder(ImageFormat.Jpeg);
            using (var parameters = new EncoderParameters(1))
            {
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                image.Save(path, encoder, parameters);
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            foreach (var codec in ImageCodecInfo.GetImageDecoders())
                if (codec.FormatID == format.Guid) return codec;

            throw new InvalidOperationException("No JPEG encoder is available.");
        }
    }
}
