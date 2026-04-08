using System.Drawing;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll.Algorithms;

public sealed class OverlapMatcher : IOverlapMatcher
{
    private readonly double _matchThreshold;
    private readonly double _excellentMatchThreshold;

    public OverlapMatcher(double matchThreshold = 0.04, double excellentMatchThreshold = 0.01)
    {
        _matchThreshold = matchThreshold;
        _excellentMatchThreshold = excellentMatchThreshold;
    }

    public OverlapResult FindOverlap(
        ReadOnlySpan<byte> previousBand,
        ReadOnlySpan<byte> currentBand,
        int width,
        int height,
        ScrollDirection direction)
    {
        if (previousBand.SequenceEqual(currentBand))
        {
            return OverlapResult.Identical();
        }

        var stride = width * PixelBuffer.BytesPerPixel;
        var previous = new PixelBufferSnapshot(width, height, stride, previousBand.ToArray());
        var current = new PixelBufferSnapshot(width, height, stride, currentBand.ToArray());

        var primaryAxisLength = direction == ScrollDirection.Vertical ? height : width;
        var best = OverlapResult.NoMatch;

        for (var overlap = primaryAxisLength - 1; overlap >= 1; overlap--)
        {
            var previousSlice = direction == ScrollDirection.Vertical
                ? PixelBuffer.ExtractSubRectangle(previous, new Rectangle(0, height - overlap, width, overlap))
                : PixelBuffer.ExtractSubRectangle(previous, new Rectangle(width - overlap, 0, overlap, height));

            var currentSlice = direction == ScrollDirection.Vertical
                ? PixelBuffer.ExtractSubRectangle(current, new Rectangle(0, 0, width, overlap))
                : PixelBuffer.ExtractSubRectangle(current, new Rectangle(0, 0, overlap, height));

            var difference = PixelBuffer.ComputeNormalizedDifference(previousSlice.Pixels, currentSlice.Pixels);
            if (difference > _matchThreshold)
            {
                continue;
            }

            var confidence = 1d - difference;
            best = new OverlapResult(overlap, false, confidence);

            if (difference <= _excellentMatchThreshold)
            {
                break;
            }
        }

        return best;
    }
}
