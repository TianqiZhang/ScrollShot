using System.Drawing;
using ScrollShot.Scroll.Models;
using ScrollShot.Scroll.Profiles.Current;
using ScrollShot.Scroll.Shared;

namespace ScrollShot.Scroll.Profiles.Signal;

public sealed class SignalHybridOverlapMatcher : IOverlapMatcher
{
    private readonly int _maxCandidates;
    private readonly double _matchThreshold;
    private readonly double _excellentMatchThreshold;
    private readonly double _crossAxisCropFraction;
    private readonly int _maxCrossAxisCropPixels;
    private readonly OverlapMatcher _fallbackMatcher;

    public SignalHybridOverlapMatcher(
        int maxCandidates = 16,
        double matchThreshold = 0.04,
        double excellentMatchThreshold = 0.01,
        double crossAxisCropFraction = 0.05,
        int maxCrossAxisCropPixels = 48)
    {
        _maxCandidates = maxCandidates;
        _matchThreshold = matchThreshold;
        _excellentMatchThreshold = excellentMatchThreshold;
        _crossAxisCropFraction = crossAxisCropFraction;
        _maxCrossAxisCropPixels = maxCrossAxisCropPixels;
        _fallbackMatcher = new OverlapMatcher(matchThreshold, excellentMatchThreshold, crossAxisCropFraction, maxCrossAxisCropPixels);
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

        var originalWidth = width;
        var originalHeight = height;
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

        var previousSignal = direction == ScrollDirection.Vertical
            ? RowColumnHash.ComputeRowHashes(previous.Pixels, width, height, previous.Stride)
            : RowColumnHash.ComputeColumnHashes(previous.Pixels, width, height, previous.Stride);
        var currentSignal = direction == ScrollDirection.Vertical
            ? RowColumnHash.ComputeRowHashes(current.Pixels, width, height, current.Stride)
            : RowColumnHash.ComputeColumnHashes(current.Pixels, width, height, current.Stride);
        var primaryAxisLength = direction == ScrollDirection.Vertical ? height : width;
        var crossAxisLength = direction == ScrollDirection.Vertical ? width : height;

        var candidates = SelectCandidates(previousSignal, currentSignal, primaryAxisLength, crossAxisLength);
        var best = OverlapResult.NoMatch;

        foreach (var overlap in candidates)
        {
            var difference = ComputeSliceDifference(previous, current, width, height, overlap, direction);
            if (difference > _matchThreshold)
            {
                continue;
            }

            var candidate = new OverlapResult(overlap, false, 1d - difference);
            if (candidate.OverlapPixels > best.OverlapPixels ||
                (candidate.OverlapPixels == best.OverlapPixels && candidate.Confidence > best.Confidence))
            {
                best = candidate;
            }

            if (difference <= _excellentMatchThreshold)
            {
                break;
            }
        }

        return best != OverlapResult.NoMatch
            ? best
            : _fallbackMatcher.FindOverlap(previousBand, currentBand, originalWidth, originalHeight, direction);
    }

    private IReadOnlyList<int> SelectCandidates(
        IReadOnlyList<long> previousSignal,
        IReadOnlyList<long> currentSignal,
        int primaryAxisLength,
        int crossAxisLength)
    {
        return Enumerable.Range(1, primaryAxisLength - 1)
            .Select(overlap => new
            {
                Overlap = overlap,
                Difference = ComputeNormalizedSignalDifference(
                    previousSignal,
                    currentSignal,
                    primaryAxisLength - overlap,
                    overlap,
                    crossAxisLength),
            })
            .OrderBy(candidate => candidate.Difference)
            .ThenByDescending(candidate => candidate.Overlap)
            .Take(_maxCandidates)
            .OrderByDescending(candidate => candidate.Overlap)
            .Select(candidate => candidate.Overlap)
            .ToArray();
    }

    private static double ComputeNormalizedSignalDifference(
        IReadOnlyList<long> previousSignal,
        IReadOnlyList<long> currentSignal,
        int previousStart,
        int overlap,
        int crossAxisLength)
    {
        if (overlap <= 0)
        {
            return 1d;
        }

        double difference = 0;
        for (var index = 0; index < overlap; index++)
        {
            difference += Math.Abs(previousSignal[previousStart + index] - currentSignal[index]);
        }

        var denominator = overlap * crossAxisLength * PixelBuffer.BytesPerPixel * 255d;
        return denominator <= 0 ? 1d : difference / denominator;
    }

    private static double ComputeSliceDifference(
        PixelBufferSnapshot previous,
        PixelBufferSnapshot current,
        int width,
        int height,
        int overlap,
        ScrollDirection direction)
    {
        var previousSlice = direction == ScrollDirection.Vertical
            ? PixelBuffer.ExtractSubRectangle(previous, new Rectangle(0, height - overlap, width, overlap))
            : PixelBuffer.ExtractSubRectangle(previous, new Rectangle(width - overlap, 0, overlap, height));

        var currentSlice = direction == ScrollDirection.Vertical
            ? PixelBuffer.ExtractSubRectangle(current, new Rectangle(0, 0, width, overlap))
            : PixelBuffer.ExtractSubRectangle(current, new Rectangle(0, 0, overlap, height));

        return PixelBuffer.ComputeNormalizedDifference(previousSlice.Pixels, currentSlice.Pixels);
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
