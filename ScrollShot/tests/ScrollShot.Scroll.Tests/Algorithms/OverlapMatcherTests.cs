using FluentAssertions;
using ScrollShot.Scroll.Algorithms;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll.Tests.Algorithms;

public sealed class OverlapMatcherTests
{
    [Fact]
    public void FindOverlap_IdenticalBands_ReturnsIdentical()
    {
        using var bitmap = TestBitmapFactory.CreateOverlapBand(4, 4, 10);
        var snapshot = PixelBuffer.FromBitmap(bitmap);
        var matcher = new OverlapMatcher();

        var result = matcher.FindOverlap(snapshot.Pixels, snapshot.Pixels, snapshot.Width, snapshot.Height, ScrollDirection.Vertical);

        result.IsIdentical.Should().BeTrue();
    }

    [Fact]
    public void FindOverlap_OneRowScroll_ReturnsOverlapOfHeightMinusOne()
    {
        // Band with 5 rows. Current is shifted down by 1 row relative to previous.
        // Bottom 4 rows of previous == top 4 rows of current → overlap = 4.
        using var previousBitmap = TestBitmapFactory.CreateOverlapBand(4, 5, 10);
        using var currentBitmap = TestBitmapFactory.CreateOverlapBand(4, 5, 11);
        var previous = PixelBuffer.FromBitmap(previousBitmap);
        var current = PixelBuffer.FromBitmap(currentBitmap);
        var matcher = new OverlapMatcher();

        var result = matcher.FindOverlap(previous.Pixels, current.Pixels, previous.Width, previous.Height, ScrollDirection.Vertical);

        result.OverlapPixels.Should().Be(4);
        result.IsIdentical.Should().BeFalse();
        result.Confidence.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public void FindOverlap_CompletelyDifferentBands_ReturnsNoMatch()
    {
        using var previousBitmap = TestBitmapFactory.CreateSolidBitmap(4, 4, System.Drawing.Color.Red);
        using var currentBitmap = TestBitmapFactory.CreateSolidBitmap(4, 4, System.Drawing.Color.Blue);
        var previous = PixelBuffer.FromBitmap(previousBitmap);
        var current = PixelBuffer.FromBitmap(currentBitmap);
        var matcher = new OverlapMatcher();

        var result = matcher.FindOverlap(previous.Pixels, current.Pixels, 4, 4, ScrollDirection.Vertical);

        result.OverlapPixels.Should().Be(0);
        result.IsIdentical.Should().BeFalse();
    }

    [Fact]
    public void FindOverlap_IgnoresNoisySideMarginsForVerticalScroll()
    {
        using var previousBitmap = TestBitmapFactory.CreateOverlapBand(240, 6, 10);
        using var currentBitmap = TestBitmapFactory.CreateOverlapBand(240, 6, 11);
        AddNoisySideMargins(previousBitmap, leftWidth: 10, rightWidth: 10, seed: 20);
        AddNoisySideMargins(currentBitmap, leftWidth: 10, rightWidth: 10, seed: 140);
        var previous = PixelBuffer.FromBitmap(previousBitmap);
        var current = PixelBuffer.FromBitmap(currentBitmap);
        var matcher = new OverlapMatcher();

        var result = matcher.FindOverlap(previous.Pixels, current.Pixels, previous.Width, previous.Height, ScrollDirection.Vertical);

        result.OverlapPixels.Should().Be(5);
        result.Confidence.Should().BeGreaterThan(0.9);
    }

    private static void AddNoisySideMargins(System.Drawing.Bitmap bitmap, int leftWidth, int rightWidth, int seed)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < leftWidth; x++)
            {
                var value = (seed + (x * 11) + (y * 17)) % 255;
                bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(255, value, 255 - value, value / 2));
            }

            for (var x = bitmap.Width - rightWidth; x < bitmap.Width; x++)
            {
                var value = (seed + (x * 7) + (y * 13)) % 255;
                bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(255, 255 - value, value, value / 3));
            }
        }
    }
}
