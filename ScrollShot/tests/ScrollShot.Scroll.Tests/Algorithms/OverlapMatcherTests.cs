using FluentAssertions;
using ScrollShot.Scroll.Algorithms;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll.Tests.Algorithms;

public sealed class OverlapMatcherTests
{
    [Fact]
    public void FindOverlap_DetectsIdenticalBands()
    {
        using var bitmap = TestBitmapFactory.CreateOverlapBand(4, 4, 10);
        var snapshot = PixelBuffer.FromBitmap(bitmap);
        var matcher = new OverlapMatcher();

        var result = matcher.FindOverlap(snapshot.Pixels, snapshot.Pixels, snapshot.Width, snapshot.Height, ScrollDirection.Vertical);

        result.IsIdentical.Should().BeTrue();
    }

    [Fact]
    public void FindOverlap_DetectsVerticalOverlap()
    {
        using var previousBitmap = TestBitmapFactory.CreateOverlapBand(4, 5, 10);
        using var currentBitmap = TestBitmapFactory.CreateOverlapBand(4, 5, 11);
        var previous = PixelBuffer.FromBitmap(previousBitmap);
        var current = PixelBuffer.FromBitmap(currentBitmap);
        var matcher = new OverlapMatcher();

        var result = matcher.FindOverlap(previous.Pixels, current.Pixels, previous.Width, previous.Height, ScrollDirection.Vertical);

        result.OverlapPixels.Should().BeGreaterThan(0);
        result.IsIdentical.Should().BeFalse();
    }
}
