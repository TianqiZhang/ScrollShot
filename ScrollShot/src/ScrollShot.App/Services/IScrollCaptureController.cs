using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.App.Services;

internal interface IScrollCaptureController : IDisposable
{
    void Start(ScreenRect region, ScrollDirection direction);

    Task<bool> CaptureAsync(CancellationToken cancellationToken = default);

    CaptureResult Finish();
}
