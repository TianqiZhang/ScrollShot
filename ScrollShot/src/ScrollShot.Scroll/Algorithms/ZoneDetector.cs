using System.Drawing;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll.Algorithms;

public sealed class ZoneDetector : IZoneDetector
{
    private readonly double _fixedThreshold;
    private readonly int _refinementTolerancePixels;

    public ZoneDetector(double fixedThreshold = 0.02, int refinementTolerancePixels = 2)
    {
        _fixedThreshold = fixedThreshold;
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

        if (direction == ScrollDirection.Vertical)
        {
            var fixedTop = ScanTop(previousBuffer, currentBuffer);
            var fixedBottom = ScanBottom(previousBuffer, currentBuffer);
            if (fixedTop + fixedBottom >= previousBuffer.Height)
            {
                return new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, previousBuffer.Width, previousBuffer.Height));
            }

            return new ZoneLayout(
                fixedTop,
                fixedBottom,
                0,
                0,
                new ScreenRect(
                    0,
                    fixedTop,
                    previousBuffer.Width,
                    previousBuffer.Height - fixedTop - fixedBottom));
        }

        var fixedLeft = ScanLeft(previousBuffer, currentBuffer, 0, previousBuffer.Height);
        var fixedRight = ScanRight(previousBuffer, currentBuffer, 0, previousBuffer.Height);
        if (fixedLeft + fixedRight >= previousBuffer.Width)
        {
            return new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, previousBuffer.Width, previousBuffer.Height));
        }

        return new ZoneLayout(
            0,
            0,
            fixedLeft,
            fixedRight,
            new ScreenRect(
                fixedLeft,
                0,
                previousBuffer.Width - fixedLeft - fixedRight,
                previousBuffer.Height));
    }

    public ZoneLayout RefineZones(ZoneLayout existing, CapturedFrame previous, CapturedFrame current, ScrollDirection direction)
    {
        var detected = DetectZones(previous, current, direction);
        return AreLayoutsEquivalent(existing, detected) ? existing : detected;
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
}
