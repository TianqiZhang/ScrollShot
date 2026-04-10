using System.Drawing;
using ScrollShot.Scroll.Shared;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll.Profiles.Current;

public sealed class OverlapMatcher : IOverlapMatcher
{
    private readonly double _matchThreshold;
    private readonly double _excellentMatchThreshold;
    private readonly double _crossAxisCropFraction;
    private readonly int _maxCrossAxisCropPixels;

    public OverlapMatcher(
        double matchThreshold = 0.04,
        double excellentMatchThreshold = 0.01,
        double crossAxisCropFraction = 0.05,
        int maxCrossAxisCropPixels = 48)
    {
        _matchThreshold = matchThreshold;
        _excellentMatchThreshold = excellentMatchThreshold;
        _crossAxisCropFraction = crossAxisCropFraction;
        _maxCrossAxisCropPixels = maxCrossAxisCropPixels;
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
        var comparisonRectangle = GetComparisonRectangle(width, height, direction);
        if (comparisonRectangle.Width != width || comparisonRectangle.Height != height)
        {
            previous = PixelBuffer.ExtractSubRectangle(previous, comparisonRectangle);
            current = PixelBuffer.ExtractSubRectangle(current, comparisonRectangle);
            width = previous.Width;
            height = previous.Height;
        }

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

    private Rectangle GetComparisonRectangle(int width, int height, ScrollDirection direction)
    {
        var crossAxisLength = direction == ScrollDirection.Vertical ? width : height;
        var crop = Math.Min(_maxCrossAxisCropPixels, (int)Math.Floor(crossAxisLength * _crossAxisCropFraction));
        if (crop <= 0 || crossAxisLength - (crop * 2) < 32)
        {
            return new Rectangle(0, 0, width, height);
        }

        return direction == ScrollDirection.Vertical
            ? new Rectangle(crop, 0, width - (crop * 2), height)
            : new Rectangle(0, crop, width, height - (crop * 2));
    }
}
