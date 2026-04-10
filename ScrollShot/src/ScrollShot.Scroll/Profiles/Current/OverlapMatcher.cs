using System.Buffers;
using System.Drawing;
using ScrollShot.Scroll.Shared;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll.Profiles.Current;

public sealed class OverlapMatcher : IOverlapMatcher
{
    private readonly int _maxSignalCandidates;
    private readonly double _matchThreshold;
    private readonly double _excellentMatchThreshold;
    private readonly double _crossAxisCropFraction;
    private readonly int _maxCrossAxisCropPixels;

    public OverlapMatcher(
        double matchThreshold = 0.04,
        double excellentMatchThreshold = 0.01,
        double crossAxisCropFraction = 0.05,
        int maxCrossAxisCropPixels = 48,
        int maxSignalCandidates = 8)
    {
        _maxSignalCandidates = maxSignalCandidates;
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

        var comparisonRectangle = GetComparisonRectangle(width, height, direction);
        var primaryAxisLength = direction == ScrollDirection.Vertical
            ? comparisonRectangle.Height
            : comparisonRectangle.Width;
        var lumaPixelCount = width * height;
        var previousLumaBuffer = ArrayPool<byte>.Shared.Rent(lumaPixelCount);
        var currentLumaBuffer = ArrayPool<byte>.Shared.Rent(lumaPixelCount);
        var previousSignalBuffer = ArrayPool<long>.Shared.Rent(primaryAxisLength);
        var currentSignalBuffer = ArrayPool<long>.Shared.Rent(primaryAxisLength);
        var best = OverlapResult.NoMatch;

        try
        {
            var previousLuma = previousLumaBuffer.AsSpan(0, lumaPixelCount);
            var currentLuma = currentLumaBuffer.AsSpan(0, lumaPixelCount);
            var previousSignal = previousSignalBuffer.AsSpan(0, primaryAxisLength);
            var currentSignal = currentSignalBuffer.AsSpan(0, primaryAxisLength);
            ProjectToLuma(previousBand, width, height, previousLuma);
            ProjectToLuma(currentBand, width, height, currentLuma);
            BuildDirectionalSignal(previousLuma, width, height, comparisonRectangle, direction, previousSignal);
            BuildDirectionalSignal(currentLuma, width, height, comparisonRectangle, direction, currentSignal);

            var signalCandidates = SelectSignalCandidates(previousSignal, currentSignal, primaryAxisLength, direction == ScrollDirection.Vertical ? comparisonRectangle.Width : comparisonRectangle.Height);
            if (TryFindExcellentSignalCandidate(previousLuma, currentLuma, width, comparisonRectangle, direction, signalCandidates, out best))
            {
                return best;
            }

            best = FindBestOverlap(previousLuma, currentLuma, width, comparisonRectangle, direction);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(previousLumaBuffer);
            ArrayPool<byte>.Shared.Return(currentLumaBuffer);
            ArrayPool<long>.Shared.Return(previousSignalBuffer);
            ArrayPool<long>.Shared.Return(currentSignalBuffer);
        }

        return best;
    }

    private static double ComputeNormalizedDifference(
        ReadOnlySpan<byte> previousLuma,
        ReadOnlySpan<byte> currentLuma,
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

        var rowLength = width;
        long sad = 0;
        for (var row = 0; row < height; row++)
        {
            var previousOffset = ((previousY + row) * stride) + previousX;
            var currentOffset = ((currentY + row) * stride) + currentX;
            sad += PixelBuffer.ComputeSumOfAbsoluteDifferences(
                previousLuma.Slice(previousOffset, rowLength),
                currentLuma.Slice(currentOffset, rowLength));
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

    private OverlapResult FindBestOverlap(
        ReadOnlySpan<byte> previousLuma,
        ReadOnlySpan<byte> currentLuma,
        int stride,
        Rectangle comparisonRectangle,
        ScrollDirection direction)
    {
        var primaryAxisLength = direction == ScrollDirection.Vertical
            ? comparisonRectangle.Height
            : comparisonRectangle.Width;
        var best = OverlapResult.NoMatch;

        for (var overlap = primaryAxisLength - 1; overlap >= 1; overlap--)
        {
            var difference = ComputeOverlapDifference(previousLuma, currentLuma, stride, comparisonRectangle, direction, overlap);
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

    private bool TryFindExcellentSignalCandidate(
        ReadOnlySpan<byte> previousLuma,
        ReadOnlySpan<byte> currentLuma,
        int stride,
        Rectangle comparisonRectangle,
        ScrollDirection direction,
        IReadOnlyList<int> signalCandidates,
        out OverlapResult best)
    {
        best = OverlapResult.NoMatch;
        foreach (var overlap in signalCandidates)
        {
            var difference = ComputeOverlapDifference(previousLuma, currentLuma, stride, comparisonRectangle, direction, overlap);
            if (difference > _excellentMatchThreshold)
            {
                continue;
            }

            var candidate = new OverlapResult(overlap, false, 1d - difference);
            if (candidate.OverlapPixels > best.OverlapPixels ||
                (candidate.OverlapPixels == best.OverlapPixels && candidate.Confidence > best.Confidence))
            {
                best = candidate;
            }
        }

        return best != OverlapResult.NoMatch;
    }

    private double ComputeOverlapDifference(
        ReadOnlySpan<byte> previousLuma,
        ReadOnlySpan<byte> currentLuma,
        int stride,
        Rectangle comparisonRectangle,
        ScrollDirection direction,
        int overlap)
    {
        return direction == ScrollDirection.Vertical
            ? ComputeNormalizedDifference(
                previousLuma,
                currentLuma,
                stride,
                comparisonRectangle.X,
                comparisonRectangle.Y + comparisonRectangle.Height - overlap,
                comparisonRectangle.X,
                comparisonRectangle.Y,
                comparisonRectangle.Width,
                overlap)
            : ComputeNormalizedDifference(
                previousLuma,
                currentLuma,
                stride,
                comparisonRectangle.X + comparisonRectangle.Width - overlap,
                comparisonRectangle.Y,
                comparisonRectangle.X,
                comparisonRectangle.Y,
                overlap,
                comparisonRectangle.Height);
    }

    private IReadOnlyList<int> SelectSignalCandidates(
        ReadOnlySpan<long> previousSignal,
        ReadOnlySpan<long> currentSignal,
        int primaryAxisLength,
        int crossAxisLength)
    {
        if (_maxSignalCandidates <= 0 || primaryAxisLength <= 1)
        {
            return Array.Empty<int>();
        }

        var candidateCount = Math.Min(_maxSignalCandidates, primaryAxisLength - 1);
        var topCandidates = new SignalCandidate[candidateCount];
        var topCount = 0;

        for (var overlap = 1; overlap < primaryAxisLength; overlap++)
        {
            var difference = ComputeNormalizedSignalDifference(
                previousSignal,
                currentSignal,
                primaryAxisLength - overlap,
                overlap,
                crossAxisLength);
            var candidate = new SignalCandidate(overlap, difference);
            if (topCount < candidateCount)
            {
                topCandidates[topCount++] = candidate;
                continue;
            }

            var worstIndex = 0;
            for (var index = 1; index < topCount; index++)
            {
                if (IsWorseCandidate(topCandidates[index], topCandidates[worstIndex]))
                {
                    worstIndex = index;
                }
            }

            if (IsBetterCandidate(candidate, topCandidates[worstIndex]))
            {
                topCandidates[worstIndex] = candidate;
            }
        }

        Array.Sort(topCandidates, 0, topCount, SignalCandidateComparer.Instance);
        var overlaps = new int[topCount];
        for (var index = 0; index < topCount; index++)
        {
            overlaps[index] = topCandidates[index].Overlap;
        }

        return overlaps;
    }

    private static void BuildDirectionalSignal(
        ReadOnlySpan<byte> lumaPixels,
        int width,
        int height,
        Rectangle comparisonRectangle,
        ScrollDirection direction,
        Span<long> signal)
    {
        signal.Clear();
        if (direction == ScrollDirection.Vertical)
        {
            for (var row = 0; row < comparisonRectangle.Height; row++)
            {
                var rowOffset = ((comparisonRectangle.Y + row) * width) + comparisonRectangle.X;
                long sum = 0;
                for (var column = 0; column < comparisonRectangle.Width; column++)
                {
                    sum += lumaPixels[rowOffset + column];
                }

                signal[row] = sum;
            }

            return;
        }

        for (var column = 0; column < comparisonRectangle.Width; column++)
        {
            long sum = 0;
            var columnX = comparisonRectangle.X + column;
            for (var row = 0; row < comparisonRectangle.Height; row++)
            {
                sum += lumaPixels[((comparisonRectangle.Y + row) * width) + columnX];
            }

            signal[column] = sum;
        }
    }

    private static double ComputeNormalizedSignalDifference(
        ReadOnlySpan<long> previousSignal,
        ReadOnlySpan<long> currentSignal,
        int previousStart,
        int overlap,
        int crossAxisLength)
    {
        if (overlap <= 0)
        {
            return 1d;
        }

        long difference = 0;
        for (var index = 0; index < overlap; index++)
        {
            difference += Math.Abs(previousSignal[previousStart + index] - currentSignal[index]);
        }

        var denominator = overlap * crossAxisLength * 255d;
        return denominator <= 0 ? 1d : difference / denominator;
    }

    private static bool IsBetterCandidate(SignalCandidate candidate, SignalCandidate existing)
    {
        return candidate.Difference < existing.Difference ||
               (candidate.Difference == existing.Difference && candidate.Overlap > existing.Overlap);
    }

    private static bool IsWorseCandidate(SignalCandidate candidate, SignalCandidate existing)
    {
        return candidate.Difference > existing.Difference ||
               (candidate.Difference == existing.Difference && candidate.Overlap < existing.Overlap);
    }

    private static void ProjectToLuma(ReadOnlySpan<byte> bgraPixels, int width, int height, Span<byte> lumaPixels)
    {
        var pixelCount = width * height;
        for (var index = 0; index < pixelCount; index++)
        {
            var pixelOffset = index * PixelBuffer.BytesPerPixel;
            var blue = bgraPixels[pixelOffset];
            var green = bgraPixels[pixelOffset + 1];
            var red = bgraPixels[pixelOffset + 2];
            lumaPixels[index] = (byte)(((red * 77) + (green * 150) + (blue * 29)) >> 8);
        }
    }

    private readonly record struct SignalCandidate(int Overlap, double Difference);

    private sealed class SignalCandidateComparer : IComparer<SignalCandidate>
    {
        public static readonly SignalCandidateComparer Instance = new();

        public int Compare(SignalCandidate x, SignalCandidate y)
        {
            var differenceComparison = x.Difference.CompareTo(y.Difference);
            return differenceComparison != 0
                ? differenceComparison
                : y.Overlap.CompareTo(x.Overlap);
        }
    }
}
