using System.Drawing;
using FluentAssertions;
using ScrollShot.Capture.Models;
using ScrollShot.Editor.Composition;
using ScrollShot.Editor.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Editor.Tests.Composition;

public sealed class ImageCompositorTests
{
    private readonly ImageCompositor _compositor = new();

    [Fact]
    public void Compose_VerticalCapture_IncludesSegmentsAndChrome()
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

        using var composed = _compositor.Compose(result, EditState.Default);

        composed.Width.Should().Be(4);
        composed.Height.Should().Be(8); // 2 top + 3 seg1 + 2 seg2 + 1 bottom
        composed.GetPixel(1, 2).ToArgb().Should().Be(Color.Red.ToArgb());
        composed.GetPixel(1, 5).ToArgb().Should().Be(Color.Yellow.ToArgb());
        composed.GetPixel(1, 7).ToArgb().Should().Be(Color.Green.ToArgb());
    }

    [Fact]
    public void Compose_ChromeToggleOff_ExcludesFixedRegions()
    {
        using var fixedTop = CreateSolidBitmap(4, 2, Color.Blue);
        using var fixedBottom = CreateSolidBitmap(4, 1, Color.Green);
        using var segment = CreateSolidBitmap(4, 5, Color.Red);

        var result = new CaptureResult(
            new[] { new ScrollSegment((Bitmap)segment.Clone(), 0) },
            new ZoneLayout(2, 1, 0, 0, new ScreenRect(0, 2, 4, 5)),
            ScrollDirection.Vertical,
            4,
            8,
            fixedTopBitmap: (Bitmap)fixedTop.Clone(),
            fixedBottomBitmap: (Bitmap)fixedBottom.Clone());

        using var composed = _compositor.Compose(result, EditState.Default with { IncludeChrome = false });

        composed.Width.Should().Be(4);
        composed.Height.Should().Be(5); // only the segment, no chrome
    }

    [Fact]
    public void Compose_TrimRemovesHeadAndTail()
    {
        using var segment = CreateSolidBitmap(3, 10, Color.Red);
        var result = CreateSingleSegmentResult(segment, 3, 10);

        using var composed = _compositor.Compose(
            result,
            EditState.Default with { TrimRange = new TrimRange(3, 2) });

        composed.Height.Should().Be(5); // 10 - 3 head - 2 tail
    }

    [Fact]
    public void Compose_CutRemovesMiddleSection()
    {
        using var segment = CreateSolidBitmap(3, 10, Color.Red);
        var result = CreateSingleSegmentResult(segment, 3, 10);

        using var composed = _compositor.Compose(
            result,
            EditState.Default with { CutRanges = new[] { new CutRange(3, 7) } });

        composed.Height.Should().Be(6); // 10 - 4 cut
    }

    [Fact]
    public void Compose_TrimAndCutCombine()
    {
        using var segment = CreateSolidBitmap(3, 10, Color.Red);
        var result = CreateSingleSegmentResult(segment, 3, 10);

        using var composed = _compositor.Compose(
            result,
            EditState.Default with
            {
                TrimRange = new TrimRange(2, 1),
                CutRanges = new[] { new CutRange(4, 6) },
            });

        composed.Height.Should().Be(5); // 10 - 2 head - 1 tail - 2 cut
    }

    [Fact]
    public void Compose_AdjacentCutsAreMerged()
    {
        using var segment = CreateSolidBitmap(3, 20, Color.Red);
        var result = CreateSingleSegmentResult(segment, 3, 20);

        using var composed = _compositor.Compose(
            result,
            EditState.Default with
            {
                CutRanges = new[] { new CutRange(5, 10), new CutRange(10, 15) },
            });

        composed.Height.Should().Be(10); // 20 - 10 merged cut
    }

    [Fact]
    public void Compose_CropReducesToSpecifiedRectangle()
    {
        using var segment = CreateSolidBitmap(10, 10, Color.Red);
        var result = CreateSingleSegmentResult(segment, 10, 10);

        using var composed = _compositor.Compose(
            result,
            EditState.Default with { CropRect = new CropRect(1, 2, 5, 4) });

        composed.Width.Should().Be(5);
        composed.Height.Should().Be(4);
    }

    [Fact]
    public void Compose_PreservesPixelWidth_WhenSourceBitmapHasNonDefaultDpi()
    {
        using var segment = CreateSolidBitmap(10, 4, Color.Red);
        segment.SetResolution(168, 168);
        var result = CreateSingleSegmentResult(segment, 10, 4);

        using var composed = _compositor.Compose(result, EditState.Default);

        composed.Width.Should().Be(10);
        composed.Height.Should().Be(4);
        composed.GetPixel(9, 2).ToArgb().Should().Be(Color.Red.ToArgb());
    }

    [Fact]
    public void Compose_UsesSegmentOffsetsAsAuthoritativePlacement()
    {
        using var segmentOne = CreateSolidBitmap(3, 4, Color.Red);
        using var segmentTwo = CreateSolidBitmap(3, 4, Color.Blue);
        var result = new CaptureResult(
            new[]
            {
                new ScrollSegment((Bitmap)segmentOne.Clone(), 1),
                new ScrollSegment((Bitmap)segmentTwo.Clone(), 0),
            },
            new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 4)),
            ScrollDirection.Vertical,
            3,
            5);

        using var composed = _compositor.Compose(result, EditState.Default);

        composed.Height.Should().Be(5);
        composed.GetPixel(1, 0).ToArgb().Should().Be(Color.Blue.ToArgb());
        composed.GetPixel(1, 4).ToArgb().Should().Be(Color.Red.ToArgb());
    }

    private static CaptureResult CreateSingleSegmentResult(Bitmap segment, int width, int height)
    {
        return new CaptureResult(
            new[] { new ScrollSegment((Bitmap)segment.Clone(), 0) },
            new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, width, height)),
            ScrollDirection.Vertical,
            width,
            height);
    }

    private static Bitmap CreateSolidBitmap(int width, int height, Color color)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }
}
