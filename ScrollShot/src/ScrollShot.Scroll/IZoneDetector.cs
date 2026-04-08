using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll;

public interface IZoneDetector
{
    ZoneLayout DetectZones(CapturedFrame previous, CapturedFrame current, ScrollDirection direction);

    ZoneLayout RefineZones(ZoneLayout existing, CapturedFrame previous, CapturedFrame current, ScrollDirection direction);
}
