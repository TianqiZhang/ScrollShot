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
    public void DetectZones_IdenticalFrames_ReturnsFullFrameAsScrollBand()
    {
        using var bitmap = TestBitmapFactory.CreateVerticalScrollFrame(width: 4, height: 6, topFixed: 0, bottomFixed: 0, scrollOffset: 0);
        using var previous = new CapturedFrame((Bitmap)bitmap.Clone(), new ScreenRect(0, 0, 4, 6), DateTimeOffset.UtcNow);
        using var current = new CapturedFrame((Bitmap)bitmap.Clone(), new ScreenRect(0, 0, 4, 6), DateTimeOffset.UtcNow);
        var detector = new ZoneDetector();

        var result = detector.DetectZones(previous, current, ScrollDirection.Vertical);

        // When frames are identical, everything is "fixed" — the guard returns full frame as scroll band
        result.ScrollBand.Width.Should().Be(4);
        result.ScrollBand.Height.Should().Be(6);
    }

    [Fact]
    public void DetectZones_CompletelyDifferentFrames_ReturnsNoFixedRegions()
    {
        using var previous = TestBitmapFactory.CreateSolidBitmap(4, 6, Color.Red);
        using var current = TestBitmapFactory.CreateSolidBitmap(4, 6, Color.Blue);
        using var prevFrame = new CapturedFrame(previous, new ScreenRect(0, 0, 4, 6), DateTimeOffset.UtcNow);
        using var currFrame = new CapturedFrame(current, new ScreenRect(0, 0, 4, 6), DateTimeOffset.UtcNow);
        var detector = new ZoneDetector();

        var result = detector.DetectZones(prevFrame, currFrame, ScrollDirection.Vertical);

        result.FixedTop.Should().Be(0);
        result.FixedBottom.Should().Be(0);
        result.ScrollBand.Should().Be(new ScreenRect(0, 0, 4, 6));
    }

    [Fact]
    public void DetectZones_VerticalScroll_IgnoresStableSideMarginsThatBelongToScrollableContent()
    {
        using var previousBitmap = TestBitmapFactory.CreateVerticalScrollFrameWithStableSideMargins(width: 12, height: 10, sideMargin: 3, scrollOffset: 0);
        using var currentBitmap = TestBitmapFactory.CreateVerticalScrollFrameWithStableSideMargins(width: 12, height: 10, sideMargin: 3, scrollOffset: 1);
        using var previous = new CapturedFrame(previousBitmap, new ScreenRect(0, 0, 12, 10), DateTimeOffset.UtcNow);
        using var current = new CapturedFrame(currentBitmap, new ScreenRect(0, 0, 12, 10), DateTimeOffset.UtcNow);
        var detector = new ZoneDetector();

        var result = detector.DetectZones(previous, current, ScrollDirection.Vertical);

        result.FixedLeft.Should().Be(0);
        result.FixedRight.Should().Be(0);
        result.ScrollBand.Should().Be(new ScreenRect(0, 0, 12, 10));
    }

    [Fact]
    public void DetectZones_VerticalScroll_FindsOnlyBottomFixedMargin()
    {
        using var previousBitmap = TestBitmapFactory.CreateVerticalScrollFrame(width: 8, height: 10, topFixed: 0, bottomFixed: 2, scrollOffset: 0);
        using var currentBitmap = TestBitmapFactory.CreateVerticalScrollFrame(width: 8, height: 10, topFixed: 0, bottomFixed: 2, scrollOffset: 1);
        using var previous = new CapturedFrame(previousBitmap, new ScreenRect(0, 0, 8, 10), DateTimeOffset.UtcNow);
        using var current = new CapturedFrame(currentBitmap, new ScreenRect(0, 0, 8, 10), DateTimeOffset.UtcNow);
        var detector = new ZoneDetector();

        var result = detector.DetectZones(previous, current, ScrollDirection.Vertical);

        result.FixedTop.Should().Be(0);
        result.FixedBottom.Should().Be(2);
        result.FixedLeft.Should().Be(0);
        result.FixedRight.Should().Be(0);
        result.ScrollBand.Should().Be(new ScreenRect(0, 0, 8, 8));
    }

    [Fact]
    public void DetectZones_VerticalScroll_CanFindFixedSidePanels()
    {
        using var previousBitmap = TestBitmapFactory.CreateVerticalScrollFrameWithFixedSidePanels(width: 12, height: 10, leftFixed: 2, rightFixed: 1, scrollOffset: 0);
        using var currentBitmap = TestBitmapFactory.CreateVerticalScrollFrameWithFixedSidePanels(width: 12, height: 10, leftFixed: 2, rightFixed: 1, scrollOffset: 1);
        using var previous = new CapturedFrame(previousBitmap, new ScreenRect(0, 0, 12, 10), DateTimeOffset.UtcNow);
        using var current = new CapturedFrame(currentBitmap, new ScreenRect(0, 0, 12, 10), DateTimeOffset.UtcNow);
        var detector = new ZoneDetector();

        var result = detector.DetectZones(previous, current, ScrollDirection.Vertical);

        result.FixedLeft.Should().Be(2);
        result.FixedRight.Should().Be(1);
        result.ScrollBand.Should().Be(new ScreenRect(2, 0, 9, 10));
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

    public static Bitmap CreateSolidBitmap(int width, int height, Color color)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }

    public static Bitmap CreateVerticalScrollFrameWithStableSideMargins(int width, int height, int sideMargin, int scrollOffset)
    {
        var bitmap = new Bitmap(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = x < sideMargin || x >= width - sideMargin
                    ? Color.Black
                    : Color.FromArgb(255, ((y + scrollOffset) * 31) % 255, (x * 19) % 255, ((y + scrollOffset) * 13 + x * 7) % 255);

                bitmap.SetPixel(x, y, color);
            }
        }

        return bitmap;
    }

    public static Bitmap CreateVerticalScrollFrameWithFixedSidePanels(int width, int height, int leftFixed, int rightFixed, int scrollOffset)
    {
        var bitmap = new Bitmap(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                Color color;
                if (x < leftFixed)
                {
                    color = Color.FromArgb(255, 40 + (y * 15) % 180, 80, 140 + (y * 9) % 100);
                }
                else if (x >= width - rightFixed)
                {
                    color = Color.FromArgb(255, 90, 30 + (y * 17) % 170, 180 - (y * 11) % 120);
                }
                else
                {
                    color = Color.FromArgb(255, (x * 20) % 255, ((y + scrollOffset) * 30) % 255, 80);
                }

                bitmap.SetPixel(x, y, color);
            }
        }

        return bitmap;
    }
}
