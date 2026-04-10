using System.Buffers;
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
        var lumaPixelCount = width * height;
        var previousLumaBuffer = ArrayPool<byte>.Shared.Rent(lumaPixelCount);
        var currentLumaBuffer = ArrayPool<byte>.Shared.Rent(lumaPixelCount);
        var best = OverlapResult.NoMatch;

        try
        {
            var previousLuma = previousLumaBuffer.AsSpan(0, lumaPixelCount);
            var currentLuma = currentLumaBuffer.AsSpan(0, lumaPixelCount);
            ProjectToLuma(previousBand, width, height, previousLuma);
            ProjectToLuma(currentBand, width, height, currentLuma);

            for (var overlap = primaryAxisLength - 1; overlap >= 1; overlap--)
            {
                var difference = direction == ScrollDirection.Vertical
                    ? ComputeNormalizedDifference(
                        previousLuma,
                        currentLuma,
                        width,
                        comparisonRectangle.X,
                        comparisonRectangle.Y + comparisonRectangle.Height - overlap,
                        comparisonRectangle.X,
                        comparisonRectangle.Y,
                        comparisonRectangle.Width,
                        overlap)
                    : ComputeNormalizedDifference(
                        previousLuma,
                        currentLuma,
                        width,
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
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(previousLumaBuffer);
            ArrayPool<byte>.Shared.Return(currentLumaBuffer);
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
}
