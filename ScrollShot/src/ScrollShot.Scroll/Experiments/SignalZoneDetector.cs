using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Algorithms;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll.Experiments;

public sealed class SignalZoneDetector : IZoneDetector
{
    private readonly double _fixedThreshold;
    private readonly int _transitionRunLength;
    private readonly int _smoothingRadius;
    private readonly int _refinementTolerancePixels;

    public SignalZoneDetector(
        double fixedThreshold = 0.02,
        int transitionRunLength = 2,
        int smoothingRadius = 0,
        int refinementTolerancePixels = 2)
    {
        _fixedThreshold = fixedThreshold;
        _transitionRunLength = transitionRunLength;
        _smoothingRadius = smoothingRadius;
        _refinementTolerancePixels = refinementTolerancePixels;
    }

    public ZoneLayout DetectZones(CapturedFrame previous, CapturedFrame current, ScrollDirection direction)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var previousBuffer = PixelBuffer.FromBitmap(previous.Bitmap);
        var currentBuffer = PixelBuffer.FromBitmap(current.Bitmap);
        if (previousBuffer.Width != currentBuffer.Width || previousBuffer.Height != currentBuffer.Height)
        {
            throw new ArgumentException("Frames must have the same dimensions.");
        }

        return direction == ScrollDirection.Vertical
            ? DetectVertical(previousBuffer, currentBuffer)
            : DetectHorizontal(previousBuffer, currentBuffer);
    }

    public ZoneLayout RefineZones(ZoneLayout existing, CapturedFrame previous, CapturedFrame current, ScrollDirection direction)
    {
        var detected = DetectZones(previous, current, direction);
        return AreLayoutsEquivalent(existing, detected) ? existing : detected;
    }

    private ZoneLayout DetectVertical(PixelBufferSnapshot previous, PixelBufferSnapshot current)
    {
        var rowSignal = Smooth(ComputeRowSignal(previous, current, startColumn: 0, columnCount: previous.Width));
        var fixedTop = FindStableExtentFromStart(rowSignal);
        var fixedBottom = FindStableExtentFromEnd(rowSignal);
        if (fixedTop + fixedBottom >= previous.Height)
        {
            return new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, previous.Width, previous.Height));
        }

        var scrollBandTop = fixedTop;
        var scrollBandHeight = previous.Height - fixedTop - fixedBottom;
        var columnSignal = Smooth(ComputeColumnSignal(previous, current, scrollBandTop, scrollBandHeight));
        var fixedLeft = FindStableExtentFromStart(columnSignal);
        var fixedRight = FindStableExtentFromEnd(columnSignal);
        if (fixedLeft + fixedRight >= previous.Width)
        {
            return new ZoneLayout(fixedTop, fixedBottom, 0, 0, new ScreenRect(0, fixedTop, previous.Width, scrollBandHeight));
        }

        return new ZoneLayout(
            fixedTop,
            fixedBottom,
            fixedLeft,
            fixedRight,
            new ScreenRect(
                fixedLeft,
                fixedTop,
                previous.Width - fixedLeft - fixedRight,
                scrollBandHeight));
    }

    private ZoneLayout DetectHorizontal(PixelBufferSnapshot previous, PixelBufferSnapshot current)
    {
        var columnSignal = Smooth(ComputeColumnSignal(previous, current, startRow: 0, rowCount: previous.Height));
        var fixedLeft = FindStableExtentFromStart(columnSignal);
        var fixedRight = FindStableExtentFromEnd(columnSignal);
        if (fixedLeft + fixedRight >= previous.Width)
        {
            return new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, previous.Width, previous.Height));
        }

        var scrollBandLeft = fixedLeft;
        var scrollBandWidth = previous.Width - fixedLeft - fixedRight;
        var rowSignal = Smooth(ComputeRowSignal(previous, current, scrollBandLeft, scrollBandWidth));
        var fixedTop = FindStableExtentFromStart(rowSignal);
        var fixedBottom = FindStableExtentFromEnd(rowSignal);
        if (fixedTop + fixedBottom >= previous.Height)
        {
            return new ZoneLayout(0, 0, fixedLeft, fixedRight, new ScreenRect(fixedLeft, 0, scrollBandWidth, previous.Height));
        }

        return new ZoneLayout(
            fixedTop,
            fixedBottom,
            fixedLeft,
            fixedRight,
            new ScreenRect(
                fixedLeft,
                fixedTop,
                scrollBandWidth,
                previous.Height - fixedTop - fixedBottom));
    }

    private bool AreLayoutsEquivalent(ZoneLayout existing, ZoneLayout detected)
    {
        return Math.Abs(existing.FixedTop - detected.FixedTop) <= _refinementTolerancePixels &&
               Math.Abs(existing.FixedBottom - detected.FixedBottom) <= _refinementTolerancePixels &&
               Math.Abs(existing.FixedLeft - detected.FixedLeft) <= _refinementTolerancePixels &&
               Math.Abs(existing.FixedRight - detected.FixedRight) <= _refinementTolerancePixels &&
               Math.Abs(existing.ScrollBand.X - detected.ScrollBand.X) <= _refinementTolerancePixels &&
               Math.Abs(existing.ScrollBand.Y - detected.ScrollBand.Y) <= _refinementTolerancePixels &&
               Math.Abs(existing.ScrollBand.Width - detected.ScrollBand.Width) <= _refinementTolerancePixels &&
               Math.Abs(existing.ScrollBand.Height - detected.ScrollBand.Height) <= _refinementTolerancePixels;
    }

    private double[] ComputeRowSignal(PixelBufferSnapshot previous, PixelBufferSnapshot current, int startColumn, int columnCount)
    {
        var signal = new double[previous.Height];
        for (var row = 0; row < previous.Height; row++)
        {
            signal[row] = ComputeRowDifference(previous, current, row, startColumn, columnCount);
        }

        return signal;
    }

    private double[] ComputeColumnSignal(PixelBufferSnapshot previous, PixelBufferSnapshot current, int startRow, int rowCount)
    {
        var signal = new double[previous.Width];
        for (var column = 0; column < previous.Width; column++)
        {
            signal[column] = PixelBuffer.ComputeColumnDifference(previous, current, column, startRow, rowCount);
        }

        return signal;
    }

    private double[] Smooth(IReadOnlyList<double> signal)
    {
        if (_smoothingRadius <= 0 || signal.Count <= 2)
        {
            return signal.ToArray();
        }

        var smoothed = new double[signal.Count];
        for (var index = 0; index < signal.Count; index++)
        {
            var start = Math.Max(0, index - _smoothingRadius);
            var end = Math.Min(signal.Count - 1, index + _smoothingRadius);
            double sum = 0;
            for (var cursor = start; cursor <= end; cursor++)
            {
                sum += signal[cursor];
            }

            smoothed[index] = sum / (end - start + 1);
        }

        return smoothed;
    }

    private int FindStableExtentFromStart(IReadOnlyList<double> signal)
    {
        var stableExtent = 0;
        var unstableRun = 0;

        for (var index = 0; index < signal.Count; index++)
        {
            if (signal[index] <= _fixedThreshold)
            {
                stableExtent = index + 1;
                unstableRun = 0;
                continue;
            }

            unstableRun++;
            if (unstableRun >= _transitionRunLength)
            {
                break;
            }
        }

        return stableExtent;
    }

    private int FindStableExtentFromEnd(IReadOnlyList<double> signal)
    {
        var stableExtent = 0;
        var unstableRun = 0;

        for (var index = signal.Count - 1; index >= 0; index--)
        {
            if (signal[index] <= _fixedThreshold)
            {
                stableExtent++;
                unstableRun = 0;
                continue;
            }

            unstableRun++;
            if (unstableRun >= _transitionRunLength)
            {
                break;
            }
        }

        return stableExtent;
    }

    private static double ComputeRowDifference(
        PixelBufferSnapshot previous,
        PixelBufferSnapshot current,
        int row,
        int startColumn,
        int columnCount)
    {
        var byteOffset = startColumn * PixelBuffer.BytesPerPixel;
        var byteLength = columnCount * PixelBuffer.BytesPerPixel;
        var previousOffset = (row * previous.Stride) + byteOffset;
        var currentOffset = (row * current.Stride) + byteOffset;
        return PixelBuffer.ComputeNormalizedDifference(
            previous.Pixels.AsSpan(previousOffset, byteLength),
            current.Pixels.AsSpan(currentOffset, byteLength));
    }
}
