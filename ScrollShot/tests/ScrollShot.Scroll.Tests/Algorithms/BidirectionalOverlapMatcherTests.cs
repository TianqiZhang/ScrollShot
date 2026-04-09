using FluentAssertions;
using ScrollShot.Scroll.Algorithms;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll.Tests.Algorithms;

public sealed class BidirectionalOverlapMatcherTests
{
    [Fact]
    public void FindOverlap_DownwardProgression_PrefersAppendAfter()
    {
        using var previousBitmap = TestBitmapFactory.CreateOverlapBand(4, 5, 10);
        using var currentBitmap = TestBitmapFactory.CreateOverlapBand(4, 5, 11);
        var previous = PixelBuffer.FromBitmap(previousBitmap);
        var current = PixelBuffer.FromBitmap(currentBitmap);
        var matcher = new BidirectionalOverlapMatcher(new OverlapMatcher());

        var result = matcher.FindOverlap(previous.Pixels, current.Pixels, previous.Width, previous.Height, ScrollDirection.Vertical);

        result.Placement.Should().Be(ScrollPlacement.AppendAfter);
        result.OverlapPixels.Should().Be(4);
    }

    [Fact]
    public void FindOverlap_UpwardProgression_PrefersPrependBefore()
    {
        using var previousBitmap = TestBitmapFactory.CreateOverlapBand(4, 5, 10);
        using var currentBitmap = TestBitmapFactory.CreateOverlapBand(4, 5, 9);
        var previous = PixelBuffer.FromBitmap(previousBitmap);
        var current = PixelBuffer.FromBitmap(currentBitmap);
        var matcher = new BidirectionalOverlapMatcher(new OverlapMatcher());

        var result = matcher.FindOverlap(previous.Pixels, current.Pixels, previous.Width, previous.Height, ScrollDirection.Vertical);

        result.Placement.Should().Be(ScrollPlacement.PrependBefore);
        result.OverlapPixels.Should().Be(4);
    }
}
