using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll;

public interface IBidirectionalOverlapMatcher
{
    DirectionalOverlapResult FindOverlap(
        ReadOnlySpan<byte> previousBand,
        ReadOnlySpan<byte> currentBand,
        int width,
        int height,
        ScrollDirection direction);
}
