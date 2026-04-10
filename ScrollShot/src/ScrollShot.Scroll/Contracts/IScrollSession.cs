using System.Drawing;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll;

public interface IScrollSession : IDisposable
{
    event Action<ScrollSegment>? SegmentAdded;

    event Action<ZoneLayout>? ZonesDetected;

    event Action<Bitmap>? PreviewUpdated;

    void Start(ScreenRect region, ScrollDirection direction);

    void ProcessFrame(CapturedFrame frame);

    void Finish();

    CaptureResult GetResult();
}
