using ScrollShot.Capture.Models;

namespace ScrollShot.Scroll.Models;

public readonly record struct ZoneLayout
{
    public ZoneLayout(int fixedTop, int fixedBottom, int fixedLeft, int fixedRight, ScreenRect scrollBand)
    {
        if (fixedTop < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedTop));
        }

        if (fixedBottom < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedBottom));
        }

        if (fixedLeft < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedLeft));
        }

        if (fixedRight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedRight));
        }

        FixedTop = fixedTop;
        FixedBottom = fixedBottom;
        FixedLeft = fixedLeft;
        FixedRight = fixedRight;
        ScrollBand = scrollBand;
    }

    public int FixedTop { get; }

    public int FixedBottom { get; }

    public int FixedLeft { get; }

    public int FixedRight { get; }

    public ScreenRect ScrollBand { get; }
}
