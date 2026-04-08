using System.Drawing;
using FluentAssertions;
using ScrollShot.Capture.Models;
using ScrollShot.Editor.Composition;
using ScrollShot.Editor.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Editor.Tests.Composition;

public sealed class ImageCompositorTests
{
    [Fact]
    public void Compose_StacksSegmentsAndChromeForVerticalCapture()
    {
        using var fixedTop = CreateSolidBitmap(4, 2, Color.Blue);
        using var fixedBottom = CreateSolidBitmap(4, 1, Color.Green);
        using var segmentOne = CreateSolidBitmap(4, 3, Color.Red);
        using var segmentTwo = CreateSolidBitmap(4, 2, Color.Yellow);

        var result = new CaptureResult(
            new[]
            {
                new ScrollSegment((Bitmap)segmentOne.Clone(), 0),
                new ScrollSegment((Bitmap)segmentTwo.Clone(), 3),
            },
            new ZoneLayout(2, 1, 0, 0, new ScreenRect(0, 2, 4, 5)),
            ScrollDirection.Vertical,
            4,
            8,
            fixedTopBitmap: (Bitmap)fixedTop.Clone(),
            fixedBottomBitmap: (Bitmap)fixedBottom.Clone());

        var compositor = new ImageCompositor();
        using var composed = compositor.Compose(result, EditState.Default);

        composed.Width.Should().Be(4);
        composed.Height.Should().Be(8);
    }

    [Fact]
    public void Compose_AppliesTrimAndCutRanges()
    {
        using var segment = CreateSolidBitmap(3, 10, Color.Red);
        var result = new CaptureResult(
            new[] { new ScrollSegment((Bitmap)segment.Clone(), 0) },
            new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 10)),
            ScrollDirection.Vertical,
            3,
            10);

        var compositor = new ImageCompositor();
        using var composed = compositor.Compose(
            result,
            EditState.Default with
            {
                TrimRange = new TrimRange(2, 1),
                CutRanges = new[] { new CutRange(4, 6) },
            });

        composed.Height.Should().Be(5);
    }

    private static Bitmap CreateSolidBitmap(int width, int height, Color color)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }
}
