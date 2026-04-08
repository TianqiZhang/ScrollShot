using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using ScrollShot.StitchingData.Models;
using ScrollShot.StitchingData.Services;
using ScrollShot.Tooling.Models;

namespace ScrollShot.Tooling.Services;

public sealed class SyntheticDatasetGenerator
{
    public StitchDatasetManifest Generate(SyntheticCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Width));
        }

        if (options.TotalHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TotalHeight));
        }

        if (options.ViewportHeight <= 0 || options.ViewportHeight > options.TotalHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ViewportHeight));
        }

        if (options.FixedTop < 0 || options.FixedBottom < 0 || options.FixedTop + options.FixedBottom >= options.ViewportHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(options.FixedTop), "Fixed regions must fit within the viewport.");
        }

        var scrollViewportHeight = options.ViewportHeight - options.FixedTop - options.FixedBottom;
        var scrollContentHeight = options.TotalHeight - options.FixedTop - options.FixedBottom;
        if (scrollViewportHeight <= 0 || scrollContentHeight <= 0 || scrollViewportHeight > scrollContentHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ViewportHeight), "Scrollable content must fit within the requested viewport and total height.");
        }

        Directory.CreateDirectory(options.OutputDirectory);
        var framesDirectory = Path.Combine(options.OutputDirectory, "frames");
        Directory.CreateDirectory(framesDirectory);

        var datasetName = string.IsNullOrWhiteSpace(options.DatasetName)
            ? "synthetic-scene"
            : options.DatasetName.Trim();
        var stepPixels = LongScreenshotSlicer.ResolveStepPixels(scrollViewportHeight, options.StepPixels, options.OverlapPixels);

        using var fixedTop = options.FixedTop > 0 ? CreateFixedTopBitmap(options) : null;
        using var fixedBottom = options.FixedBottom > 0 ? CreateFixedBottomBitmap(options) : null;
        using var scrollContent = RenderScrollableContent(options, scrollContentHeight);
        using var groundTruth = ComposeGroundTruth(options, fixedTop, scrollContent, fixedBottom);

        var groundTruthRelativePath = "groundtruth.png";
        groundTruth.Save(Path.Combine(options.OutputDirectory, groundTruthRelativePath), ImageFormat.Png);

        var offsets = LongScreenshotSlicer.BuildOffsets(scrollContent.Height, scrollViewportHeight, stepPixels);
        var frames = new List<StitchDatasetFrame>(offsets.Count);

        for (var index = 0; index < offsets.Count; index++)
        {
            var offset = offsets[index];
            using var scrollSlice = scrollContent.Clone(
                new Rectangle(0, offset, options.Width, scrollViewportHeight),
                PixelFormat.Format32bppArgb);
            using var frameBitmap = ComposeFrame(options, fixedTop, scrollSlice, fixedBottom);
            var frameFileName = $"frame_{index:D4}.png";
            var frameRelativePath = Path.Combine("frames", frameFileName);
            frameBitmap.Save(Path.Combine(options.OutputDirectory, frameRelativePath), ImageFormat.Png);

            frames.Add(new StitchDatasetFrame
            {
                Index = index,
                RelativePath = frameRelativePath,
                OffsetPixels = offset,
                ExpectedOverlapWithPreviousPixels = index == 0 ? null : scrollViewportHeight - (offset - offsets[index - 1]),
                Width = frameBitmap.Width,
                Height = frameBitmap.Height,
            });
        }

        var manifest = new StitchDatasetManifest
        {
            Name = datasetName,
            Source = "synthetic",
            ViewportWidth = options.Width,
            ViewportHeight = options.ViewportHeight,
            StepPixels = stepPixels,
            CaptureRegion = new StitchCaptureRegion
            {
                X = 0,
                Y = 0,
                Width = options.Width,
                Height = options.ViewportHeight,
            },
            CompletionReason = "generated",
            Truth = new StitchDatasetTruth
            {
                GroundTruthRelativePath = groundTruthRelativePath,
                GroundTruthWidth = groundTruth.Width,
                GroundTruthHeight = groundTruth.Height,
            },
            Frames = frames,
        };

        ManifestStore.Save(manifest, Path.Combine(options.OutputDirectory, "manifest.json"));
        return manifest;
    }

    internal static Bitmap RenderSourceImage(SyntheticCommandOptions options)
    {
        var scrollContentHeight = options.TotalHeight - options.FixedTop - options.FixedBottom;
        using var fixedTop = options.FixedTop > 0 ? CreateFixedTopBitmap(options) : null;
        using var fixedBottom = options.FixedBottom > 0 ? CreateFixedBottomBitmap(options) : null;
        using var scrollContent = RenderScrollableContent(options, scrollContentHeight);
        return ComposeGroundTruth(options, fixedTop, scrollContent, fixedBottom);
    }

    private static Bitmap ComposeGroundTruth(SyntheticCommandOptions options, Bitmap? fixedTop, Bitmap scrollContent, Bitmap? fixedBottom)
    {
        var bitmap = new Bitmap(options.Width, options.TotalHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.WhiteSmoke);
        if (fixedTop is not null)
        {
            graphics.DrawImage(fixedTop, new Rectangle(0, 0, fixedTop.Width, fixedTop.Height), new Rectangle(0, 0, fixedTop.Width, fixedTop.Height), GraphicsUnit.Pixel);
        }

        graphics.DrawImage(
            scrollContent,
            new Rectangle(0, options.FixedTop, scrollContent.Width, scrollContent.Height),
            new Rectangle(0, 0, scrollContent.Width, scrollContent.Height),
            GraphicsUnit.Pixel);

        if (fixedBottom is not null)
        {
            graphics.DrawImage(
                fixedBottom,
                new Rectangle(0, options.TotalHeight - fixedBottom.Height, fixedBottom.Width, fixedBottom.Height),
                new Rectangle(0, 0, fixedBottom.Width, fixedBottom.Height),
                GraphicsUnit.Pixel);
        }

        return bitmap;
    }

    private static Bitmap ComposeFrame(SyntheticCommandOptions options, Bitmap? fixedTop, Bitmap scrollSlice, Bitmap? fixedBottom)
    {
        var bitmap = new Bitmap(options.Width, options.ViewportHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.WhiteSmoke);

        if (fixedTop is not null)
        {
            graphics.DrawImage(fixedTop, new Rectangle(0, 0, fixedTop.Width, fixedTop.Height), new Rectangle(0, 0, fixedTop.Width, fixedTop.Height), GraphicsUnit.Pixel);
        }

        graphics.DrawImage(
            scrollSlice,
            new Rectangle(0, options.FixedTop, scrollSlice.Width, scrollSlice.Height),
            new Rectangle(0, 0, scrollSlice.Width, scrollSlice.Height),
            GraphicsUnit.Pixel);

        if (fixedBottom is not null)
        {
            graphics.DrawImage(
                fixedBottom,
                new Rectangle(0, options.ViewportHeight - fixedBottom.Height, fixedBottom.Width, fixedBottom.Height),
                new Rectangle(0, 0, fixedBottom.Width, fixedBottom.Height),
                GraphicsUnit.Pixel);
        }

        return bitmap;
    }

    private static Bitmap CreateFixedTopBitmap(SyntheticCommandOptions options)
    {
        var bitmap = new Bitmap(options.Width, options.FixedTop, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.Transparent);
        DrawFixedTop(graphics, options);
        return bitmap;
    }

    private static Bitmap CreateFixedBottomBitmap(SyntheticCommandOptions options)
    {
        var bitmap = new Bitmap(options.Width, options.FixedBottom, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.Transparent);
        DrawFixedBottom(graphics, options);
        return bitmap;
    }

    private static Bitmap RenderScrollableContent(SyntheticCommandOptions options, int scrollContentHeight)
    {
        var bitmap = new Bitmap(options.Width, scrollContentHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.WhiteSmoke);
        DrawScrollableBody(graphics, options.Width, scrollContentHeight);
        return bitmap;
    }

    private static void DrawFixedTop(Graphics graphics, SyntheticCommandOptions options)
    {
        var headerRect = new Rectangle(0, 0, options.Width, options.FixedTop);
        using var headerBrush = new LinearGradientBrush(headerRect, Color.FromArgb(255, 34, 40, 49), Color.FromArgb(255, 57, 62, 70), 0f);
        graphics.FillRectangle(headerBrush, headerRect);

        using var titleFont = new Font(FontFamily.GenericSansSerif, 18, FontStyle.Bold, GraphicsUnit.Pixel);
        using var subtitleFont = new Font(FontFamily.GenericSansSerif, 11, FontStyle.Regular, GraphicsUnit.Pixel);
        using var whiteBrush = new SolidBrush(Color.White);
        graphics.DrawString("Synthetic Scroll Fixture", titleFont, whiteBrush, new PointF(16, 10));
        graphics.DrawString("fixed-header baseline", subtitleFont, whiteBrush, new PointF(options.Width - 170, 16));

        using var accentBrush = new SolidBrush(Color.FromArgb(255, 0, 173, 181));
        graphics.FillRectangle(accentBrush, new Rectangle(options.Width - 186, 12, 8, options.FixedTop - 24));
    }

    private static void DrawFixedBottom(Graphics graphics, SyntheticCommandOptions options)
    {
        var top = 0;
        var footerRect = new Rectangle(0, top, options.Width, options.FixedBottom);
        using var brush = new SolidBrush(Color.FromArgb(255, 235, 238, 242));
        graphics.FillRectangle(brush, footerRect);
        using var pen = new Pen(Color.FromArgb(255, 180, 186, 194), 1);
        graphics.DrawLine(pen, 0, top, options.Width, top);

        using var font = new Font(FontFamily.GenericSansSerif, 11, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.FromArgb(255, 45, 52, 60));
        graphics.DrawString("Sticky footer action bar", font, textBrush, new PointF(16, top + 9));

        var buttonWidth = 92;
        var buttonHeight = Math.Max(18, options.FixedBottom - 14);
        var buttonY = top + ((options.FixedBottom - buttonHeight) / 2);
        using var buttonBrush = new SolidBrush(Color.FromArgb(255, 0, 173, 181));
        graphics.FillRoundedRectangle(buttonBrush, new Rectangle(options.Width - buttonWidth - 16, buttonY, buttonWidth, buttonHeight), 8);
        using var buttonTextBrush = new SolidBrush(Color.White);
        graphics.DrawString("Apply", font, buttonTextBrush, new PointF(options.Width - buttonWidth + 12, buttonY + 3));
    }

    private static void DrawScrollableBody(Graphics graphics, int width, int scrollContentHeight)
    {
        var bodyTop = 0;
        var bodyBottom = scrollContentHeight;
        using var bodyBrush = new SolidBrush(Color.FromArgb(255, 249, 250, 252));
        graphics.FillRectangle(bodyBrush, new Rectangle(0, bodyTop, width, bodyBottom - bodyTop));

        using var titleFont = new Font(FontFamily.GenericSansSerif, 20, FontStyle.Bold, GraphicsUnit.Pixel);
        using var bodyFont = new Font(FontFamily.GenericSansSerif, 13, FontStyle.Regular, GraphicsUnit.Pixel);
        using var smallFont = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Regular, GraphicsUnit.Pixel);
        using var titleBrush = new SolidBrush(Color.FromArgb(255, 30, 35, 42));
        using var textBrush = new SolidBrush(Color.FromArgb(255, 70, 80, 90));
        using var subtleBrush = new SolidBrush(Color.FromArgb(255, 132, 141, 151));

        graphics.DrawString("Synthetic content feed", titleFont, titleBrush, new PointF(24, bodyTop + 20));
        graphics.DrawString("Deterministic text + shapes for overlap and fixed-region baselines", bodyFont, textBrush, new PointF(24, bodyTop + 52));

        var cardTop = bodyTop + 92;
        var cardIndex = 0;
        while (cardTop < bodyBottom - 80)
        {
            var cardHeight = 96 + ((cardIndex % 3) * 20);
            if (cardTop + cardHeight > bodyBottom)
            {
                cardHeight = bodyBottom - cardTop - 20;
            }

            if (cardHeight <= 40)
            {
                break;
            }

            DrawContentCard(graphics, width, cardTop, cardHeight, cardIndex, bodyFont, smallFont, titleBrush, textBrush, subtleBrush);
            cardTop += cardHeight + 18;
            cardIndex++;
        }
    }

    private static void DrawContentCard(
        Graphics graphics,
        int width,
        int top,
        int height,
        int index,
        Font bodyFont,
        Font smallFont,
        Brush titleBrush,
        Brush textBrush,
        Brush subtleBrush)
    {
        var cardRect = new Rectangle(24, top, width - 48, height);
        var backgroundColor = index % 2 == 0
            ? Color.FromArgb(255, 255, 255, 255)
            : Color.FromArgb(255, 245, 248, 252);
        using var cardBrush = new SolidBrush(backgroundColor);
        using var borderPen = new Pen(Color.FromArgb(255, 214, 220, 227), 1);
        graphics.FillRoundedRectangle(cardBrush, cardRect, 12);
        graphics.DrawRoundedRectangle(borderPen, cardRect, 12);

        var accentWidth = 10 + ((index % 4) * 6);
        using var accentBrush = new SolidBrush(Color.FromArgb(255, (40 + (index * 27)) % 255, (90 + (index * 17)) % 255, (140 + (index * 31)) % 255));
        graphics.FillRoundedRectangle(accentBrush, new Rectangle(cardRect.X + 16, cardRect.Y + 16, accentWidth, cardRect.Height - 32), 5);

        graphics.DrawString($"Section {index:D2}", bodyFont, titleBrush, new PointF(cardRect.X + 36 + accentWidth, cardRect.Y + 14));
        graphics.DrawString($"Row marker {(index * 37) % 1000:D3}  |  block height {height}px", smallFont, subtleBrush, new PointF(cardRect.X + 36 + accentWidth, cardRect.Y + 40));

        for (var line = 0; line < 3; line++)
        {
            var lineY = cardRect.Y + 62 + (line * 16);
            var text = $"Synthetic narrative line {index:D2}-{line:D2} with token {(index * 13 + line * 17) % 97:D2}";
            graphics.DrawString(text, smallFont, textBrush, new PointF(cardRect.X + 36 + accentWidth, lineY));
        }

        var sparkTop = cardRect.Y + cardRect.Height - 28;
        for (var bar = 0; bar < 8; bar++)
        {
            var barHeight = 6 + (((index + 1) * (bar + 3)) % 18);
            var barX = cardRect.Right - 140 + (bar * 14);
            using var barBrush = new SolidBrush(Color.FromArgb(255, (60 + bar * 18) % 255, (160 + index * 23) % 255, (120 + bar * 11) % 255));
            graphics.FillRectangle(barBrush, new Rectangle(barX, sparkTop - barHeight, 8, barHeight));
        }
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectanglePath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectanglePath(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        var path = new GraphicsPath();

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
