using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Overlay;

public sealed class OverlayCaptureRequestedEventArgs : EventArgs
{
    public OverlayCaptureRequestedEventArgs(ScreenRect region, ScrollDirection? direction = null)
    {
        Region = region;
        Direction = direction;
    }

    public ScreenRect Region { get; }

    public ScrollDirection? Direction { get; }
}
