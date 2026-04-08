using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll;

public interface IOverlapMatcher
{
    OverlapResult FindOverlap(
        ReadOnlySpan<byte> previousBand,
        ReadOnlySpan<byte> currentBand,
        int width,
        int height,
        ScrollDirection direction);
}
