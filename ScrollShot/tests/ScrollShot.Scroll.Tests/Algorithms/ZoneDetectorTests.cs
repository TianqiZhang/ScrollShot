using System.Drawing;
using FluentAssertions;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Algorithms;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll.Tests.Algorithms;

public sealed class ZoneDetectorTests
{
    [Fact]
    public void DetectZones_FindsFixedTopAndBottomMargins()
    {
        using var previousBitmap = TestBitmapFactory.CreateVerticalScrollFrame(width: 6, height: 8, topFixed: 2, bottomFixed: 1, scrollOffset: 0);
        using var currentBitmap = TestBitmapFactory.CreateVerticalScrollFrame(width: 6, height: 8, topFixed: 2, bottomFixed: 1, scrollOffset: 1);
        using var previous = new CapturedFrame(previousBitmap, new ScreenRect(0, 0, 6, 8), DateTimeOffset.UtcNow);
        using var current = new CapturedFrame(currentBitmap, new ScreenRect(0, 0, 6, 8), DateTimeOffset.UtcNow);
        var detector = new ZoneDetector();

        var result = detector.DetectZones(previous, current, ScrollDirection.Vertical);

        result.FixedTop.Should().Be(2);
        result.FixedBottom.Should().Be(1);
        result.ScrollBand.Should().Be(new ScreenRect(0, 2, 6, 5));
    }

    [Fact]
    public void RefineZones_PreservesExistingLayout_WhenDifferenceIsWithinTolerance()
    {
        using var previousBitmap = TestBitmapFactory.CreateVerticalScrollFrame(width: 6, height: 8, topFixed: 2, bottomFixed: 1, scrollOffset: 0);
        using var currentBitmap = TestBitmapFactory.CreateVerticalScrollFrame(width: 6, height: 8, topFixed: 3, bottomFixed: 1, scrollOffset: 1);
        using var previous = new CapturedFrame(previousBitmap, new ScreenRect(0, 0, 6, 8), DateTimeOffset.UtcNow);
        using var current = new CapturedFrame(currentBitmap, new ScreenRect(0, 0, 6, 8), DateTimeOffset.UtcNow);
        var detector = new ZoneDetector(refinementTolerancePixels: 2);
        var existing = new ZoneLayout(2, 1, 0, 0, new ScreenRect(0, 2, 6, 5));

        var refined = detector.RefineZones(existing, previous, current, ScrollDirection.Vertical);

        refined.Should().Be(existing);
    }
}

internal static class TestBitmapFactory
{
    public static Bitmap CreateVerticalScrollFrame(int width, int height, int topFixed, int bottomFixed, int scrollOffset)
    {
        var bitmap = new Bitmap(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = y < topFixed
                    ? Color.Blue
                    : y >= height - bottomFixed
                        ? Color.Green
                        : Color.FromArgb(255, (x * 20) % 255, ((y + scrollOffset) * 30) % 255, 80);

                bitmap.SetPixel(x, y, color);
            }
        }

        return bitmap;
    }

    public static Bitmap CreateOverlapBand(int width, int height, int startingValue)
    {
        var bitmap = new Bitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = (startingValue + y + x) % 255;
                bitmap.SetPixel(x, y, Color.FromArgb(255, value, value, value));
            }
        }

        return bitmap;
    }
}
