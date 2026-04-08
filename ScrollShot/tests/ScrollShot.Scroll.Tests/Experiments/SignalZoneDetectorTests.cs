using System.Drawing;
using FluentAssertions;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Experiments;
using ScrollShot.Scroll.Models;
using ScrollShot.Scroll.Tests.Algorithms;

namespace ScrollShot.Scroll.Tests.Experiments;

public sealed class SignalZoneDetectorTests
{
    [Fact]
    public void DetectZones_FindsFixedTopAndBottomMargins_ForVerticalScroll()
    {
        using var previousBitmap = TestBitmapFactory.CreateVerticalScrollFrame(width: 6, height: 10, topFixed: 2, bottomFixed: 2, scrollOffset: 0);
        using var currentBitmap = TestBitmapFactory.CreateVerticalScrollFrame(width: 6, height: 10, topFixed: 2, bottomFixed: 2, scrollOffset: 1);
        using var previous = new CapturedFrame(previousBitmap, new ScreenRect(0, 0, 6, 10), DateTimeOffset.UtcNow);
        using var current = new CapturedFrame(currentBitmap, new ScreenRect(0, 0, 6, 10), DateTimeOffset.UtcNow);
        var detector = new SignalZoneDetector();

        var result = detector.DetectZones(previous, current, ScrollDirection.Vertical);

        result.FixedTop.Should().Be(2);
        result.FixedBottom.Should().Be(2);
        result.ScrollBand.Should().Be(new ScreenRect(0, 2, 6, 6));
    }

    [Fact]
    public void DetectZones_IdenticalFrames_ReturnsFullFrameAsScrollBand()
    {
        using var bitmap = TestBitmapFactory.CreateVerticalScrollFrame(width: 6, height: 10, topFixed: 2, bottomFixed: 2, scrollOffset: 0);
        using var previous = new CapturedFrame((Bitmap)bitmap.Clone(), new ScreenRect(0, 0, 6, 10), DateTimeOffset.UtcNow);
        using var current = new CapturedFrame((Bitmap)bitmap.Clone(), new ScreenRect(0, 0, 6, 10), DateTimeOffset.UtcNow);
        var detector = new SignalZoneDetector();

        var result = detector.DetectZones(previous, current, ScrollDirection.Vertical);

        result.ScrollBand.Should().Be(new ScreenRect(0, 0, 6, 10));
    }
}
