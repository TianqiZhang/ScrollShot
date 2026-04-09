using System.Drawing;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll.Algorithms;

public sealed class ZoneDetector : IZoneDetector
{
    private readonly double _fixedThreshold;
    private readonly double _edgeRichnessThreshold;

    public ZoneDetector(double fixedThreshold = 0.02, double edgeRichnessThreshold = 0.01)
    {
        _fixedThreshold = fixedThreshold;
        _edgeRichnessThreshold = edgeRichnessThreshold;
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

        var fixedTop = ScanTop(previousBuffer, currentBuffer);
        var fixedBottom = ScanBottom(previousBuffer, currentBuffer);
        if (fixedTop + fixedBottom >= previousBuffer.Height)
        {
            return new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, previousBuffer.Width, previousBuffer.Height));
        }

        var scrollBandTop = fixedTop;
        var scrollBandHeight = previousBuffer.Height - fixedTop - fixedBottom;
        var fixedLeft = ScanLeft(previousBuffer, currentBuffer, scrollBandTop, scrollBandHeight);
        var fixedRight = ScanRight(previousBuffer, currentBuffer, scrollBandTop, scrollBandHeight);

        if (fixedLeft > 0 && !HasRichVerticalContent(previousBuffer, 0, fixedLeft, scrollBandTop, scrollBandHeight))
        {
            fixedLeft = 0;
        }

        if (fixedRight > 0 && !HasRichVerticalContent(previousBuffer, previousBuffer.Width - fixedRight, fixedRight, scrollBandTop, scrollBandHeight))
        {
            fixedRight = 0;
        }

        if (fixedLeft + fixedRight >= previousBuffer.Width)
        {
            return new ZoneLayout(fixedTop, fixedBottom, 0, 0, new ScreenRect(0, fixedTop, previousBuffer.Width, scrollBandHeight));
        }

        return new ZoneLayout(
            fixedTop,
            fixedBottom,
            fixedLeft,
            fixedRight,
            new ScreenRect(
                fixedLeft,
                fixedTop,
                previousBuffer.Width - fixedLeft - fixedRight,
                scrollBandHeight));
    }

    private int ScanTop(PixelBufferSnapshot previous, PixelBufferSnapshot current)
    {
        var fixedTop = 0;
        for (var row = 0; row < previous.Height; row++)
        {
            if (PixelBuffer.ComputeRowDifference(previous, current, row) > _fixedThreshold)
            {
                break;
            }

            fixedTop++;
        }

        return fixedTop;
    }

    private int ScanBottom(PixelBufferSnapshot previous, PixelBufferSnapshot current)
    {
        var fixedBottom = 0;
        for (var row = previous.Height - 1; row >= 0; row--)
        {
            if (PixelBuffer.ComputeRowDifference(previous, current, row) > _fixedThreshold)
            {
                break;
            }

            fixedBottom++;
        }

        return fixedBottom;
    }

    private int ScanLeft(PixelBufferSnapshot previous, PixelBufferSnapshot current, int startRow, int rowCount)
    {
        var fixedLeft = 0;
        for (var column = 0; column < previous.Width; column++)
        {
            if (PixelBuffer.ComputeColumnDifference(previous, current, column, startRow, rowCount) > _fixedThreshold)
            {
                break;
            }

            fixedLeft++;
        }

        return fixedLeft;
    }

    private int ScanRight(PixelBufferSnapshot previous, PixelBufferSnapshot current, int startRow, int rowCount)
    {
        var fixedRight = 0;
        for (var column = previous.Width - 1; column >= 0; column--)
        {
            if (PixelBuffer.ComputeColumnDifference(previous, current, column, startRow, rowCount) > _fixedThreshold)
            {
                break;
            }

            fixedRight++;
        }

        return fixedRight;
    }

    private bool HasRichVerticalContent(PixelBufferSnapshot snapshot, int startColumn, int columnCount, int startRow, int rowCount)
    {
        if (columnCount <= 0 || rowCount <= 1)
        {
            return false;
        }

        long totalDifference = 0;
        var comparisons = 0;

        for (var y = startRow + 1; y < startRow + rowCount; y++)
        {
            for (var x = startColumn; x < startColumn + columnCount; x++)
            {
                var currentIndex = ((y * snapshot.Width) + x) * 4;
                var previousIndex = ((((y - 1) * snapshot.Width) + x) * 4);
                totalDifference += Math.Abs(snapshot.Pixels[currentIndex] - snapshot.Pixels[previousIndex]);
                totalDifference += Math.Abs(snapshot.Pixels[currentIndex + 1] - snapshot.Pixels[previousIndex + 1]);
                totalDifference += Math.Abs(snapshot.Pixels[currentIndex + 2] - snapshot.Pixels[previousIndex + 2]);
                comparisons += 3;
            }
        }

        if (comparisons == 0)
        {
            return false;
        }

        var averageDifference = totalDifference / (comparisons * 255d);
        return averageDifference >= _edgeRichnessThreshold;
    }
}
