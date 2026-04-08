using System.Drawing;

namespace ScrollShot.Scroll.Models;

public sealed class CaptureResult
{
    public CaptureResult(
        IReadOnlyList<ScrollSegment> segments,
        ZoneLayout zoneLayout,
        ScrollDirection direction,
        int totalWidth,
        int totalHeight,
        Bitmap? fixedTopBitmap = null,
        Bitmap? fixedBottomBitmap = null,
        Bitmap? fixedLeftBitmap = null,
        Bitmap? fixedRightBitmap = null)
    {
        ArgumentNullException.ThrowIfNull(segments);

        if (totalWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalWidth));
        }

        if (totalHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalHeight));
        }

        Segments = segments;
        ZoneLayout = zoneLayout;
        Direction = direction;
        TotalWidth = totalWidth;
        TotalHeight = totalHeight;
        FixedTopBitmap = fixedTopBitmap;
        FixedBottomBitmap = fixedBottomBitmap;
        FixedLeftBitmap = fixedLeftBitmap;
        FixedRightBitmap = fixedRightBitmap;
    }

    public IReadOnlyList<ScrollSegment> Segments { get; }

    public ZoneLayout ZoneLayout { get; }

    public ScrollDirection Direction { get; }

    public int TotalWidth { get; }

    public int TotalHeight { get; }

    public Bitmap? FixedTopBitmap { get; }

    public Bitmap? FixedBottomBitmap { get; }

    public Bitmap? FixedLeftBitmap { get; }

    public Bitmap? FixedRightBitmap { get; }
}
