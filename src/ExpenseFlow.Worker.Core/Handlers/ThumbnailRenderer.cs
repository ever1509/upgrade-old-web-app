using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ExpenseFlow.Worker.Core.Handlers;

/// <summary>
/// Ledger item B3: System.Drawing / GDI+ replaced with ImageSharp.
///
/// The original threw PlatformNotSupportedException on anything that was not
/// Windows. This one is pure managed code and runs anywhere.
///
/// Behaviour is deliberately identical to the .NET Framework version - 320px
/// longest edge, never upscaled, white background, JPEG quality 82, written
/// beside the original as "<name>_thumb.jpg" - so the output can be compared
/// against the baseline captured before the migration.
/// </summary>
public static class ThumbnailRenderer
{
    public const int MaxEdge = 320;
    private const int JpegQuality = 82;

    public static bool IsRenderable(string? contentType, string fileName)
    {
        if (!string.IsNullOrEmpty(contentType) &&
            contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return true;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif";
    }

    /// <summary>Writes a thumbnail beside the original and returns its relative path.</summary>
    public static async Task<string> RenderAsync(string uploadRoot, string relativeSourcePath,
                                                 CancellationToken cancellationToken = default)
    {
        var source = Path.Combine(uploadRoot, relativeSourcePath);
        if (!File.Exists(source))
            throw new FileNotFoundException("Receipt file is missing.", source);

        var directory = Path.GetDirectoryName(relativeSourcePath) ?? string.Empty;
        var thumbName = Path.GetFileNameWithoutExtension(relativeSourcePath) + "_thumb.jpg";
        var relativeTarget = string.IsNullOrEmpty(directory)
            ? thumbName
            : Path.Combine(directory, thumbName);

        var target = Path.Combine(uploadRoot, relativeTarget);

        using var image = await Image.LoadAsync<Rgba32>(source, cancellationToken);

        image.Mutate(context => context
            .Resize(new ResizeOptions
            {
                Size = new Size(MaxEdge, MaxEdge),
                // Preserves aspect ratio and never enlarges a smaller image,
                // matching the original's explicit scale clamp.
                Mode = ResizeMode.Max
            })
            // JPEG has no alpha channel; flatten onto white exactly as
            // Graphics.Clear(Color.White) did before.
            .BackgroundColor(Color.White));

        await image.SaveAsJpegAsync(target, new JpegEncoder { Quality = JpegQuality }, cancellationToken);

        return relativeTarget;
    }
}
