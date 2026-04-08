using FluentAssertions;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;
using Bitmap = System.Drawing.Bitmap;

namespace ScrollShot.Scroll.Tests.Models;

public sealed class CaptureResultTests
{
    [Fact]
    public void Constructor_StoresCaptureMetadata()
    {
        using var bitmap = new Bitmap(20, 50);
        using var segment = new ScrollSegment(bitmap, 0);
        var result = new CaptureResult(
            new[] { segment },
            new ZoneLayout(10, 5, 0, 0, new ScreenRect(0, 10, 20, 50)),
            ScrollDirection.Vertical,
            20,
            65);

        result.Segments.Should().ContainSingle();
        result.Direction.Should().Be(ScrollDirection.Vertical);
        result.TotalWidth.Should().Be(20);
        result.TotalHeight.Should().Be(65);
    }
}
