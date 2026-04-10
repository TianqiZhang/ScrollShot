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
        var comparisonRectangle = GetComparisonRectangle(width, height, direction);
        var primaryAxisLength = direction == ScrollDirection.Vertical
            ? comparisonRectangle.Height
            : comparisonRectangle.Width;
        var best = OverlapResult.NoMatch;

        for (var overlap = primaryAxisLength - 1; overlap >= 1; overlap--)
        {
            var difference = direction == ScrollDirection.Vertical
                ? ComputeNormalizedDifference(
                    previousBand,
                    currentBand,
                    stride,
                    comparisonRectangle.X,
                    comparisonRectangle.Y + comparisonRectangle.Height - overlap,
                    comparisonRectangle.X,
                    comparisonRectangle.Y,
                    comparisonRectangle.Width,
                    overlap)
                : ComputeNormalizedDifference(
                    previousBand,
                    currentBand,
                    stride,
                    comparisonRectangle.X + comparisonRectangle.Width - overlap,
                    comparisonRectangle.Y,
                    comparisonRectangle.X,
                    comparisonRectangle.Y,
                    overlap,
                    comparisonRectangle.Height);
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

    private static double ComputeNormalizedDifference(
        ReadOnlySpan<byte> previous,
        ReadOnlySpan<byte> current,
        int stride,
        int previousX,
        int previousY,
        int currentX,
        int currentY,
        int width,
        int height)
    {
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        var rowLength = width * PixelBuffer.BytesPerPixel;
        long sad = 0;
        for (var row = 0; row < height; row++)
        {
            var previousOffset = ((previousY + row) * stride) + (previousX * PixelBuffer.BytesPerPixel);
            var currentOffset = ((currentY + row) * stride) + (currentX * PixelBuffer.BytesPerPixel);
            sad += PixelBuffer.ComputeSumOfAbsoluteDifferences(
                previous.Slice(previousOffset, rowLength),
                current.Slice(currentOffset, rowLength));
        }

        return sad / (255d * rowLength * height);
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
