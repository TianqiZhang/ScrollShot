using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll.Algorithms;

public sealed class BidirectionalOverlapMatcher : IBidirectionalOverlapMatcher
{
    private readonly IOverlapMatcher _baseMatcher;

    public BidirectionalOverlapMatcher(IOverlapMatcher? baseMatcher = null)
    {
        _baseMatcher = baseMatcher ?? new OverlapMatcher();
    }

    public DirectionalOverlapResult FindOverlap(
        ReadOnlySpan<byte> previousBand,
        ReadOnlySpan<byte> currentBand,
        int width,
        int height,
        ScrollDirection direction)
    {
        var forward = _baseMatcher.FindOverlap(previousBand, currentBand, width, height, direction);
        var reverse = _baseMatcher.FindOverlap(currentBand, previousBand, width, height, direction);

        var forwardResult = new DirectionalOverlapResult(
            forward.OverlapPixels,
            forward.IsIdentical,
            forward.Confidence,
            ScrollPlacement.AppendAfter);

        var reverseResult = new DirectionalOverlapResult(
            reverse.OverlapPixels,
            reverse.IsIdentical,
            reverse.Confidence,
            ScrollPlacement.PrependBefore);

        if (forwardResult.IsIdentical && !reverseResult.IsIdentical)
        {
            return forwardResult;
        }

        if (reverseResult.IsIdentical && !forwardResult.IsIdentical)
        {
            return reverseResult;
        }

        if (forwardResult.HasMatch && !reverseResult.HasMatch)
        {
            return forwardResult;
        }

        if (reverseResult.HasMatch && !forwardResult.HasMatch)
        {
            return reverseResult;
        }

        if (forwardResult.Confidence > reverseResult.Confidence)
        {
            return forwardResult;
        }

        if (reverseResult.Confidence > forwardResult.Confidence)
        {
            return reverseResult;
        }

        if (forwardResult.OverlapPixels > reverseResult.OverlapPixels)
        {
            return forwardResult;
        }

        if (reverseResult.OverlapPixels > forwardResult.OverlapPixels)
        {
            return reverseResult;
        }

        return forwardResult.HasMatch
            ? forwardResult
            : DirectionalOverlapResult.NoMatch();
    }
}
