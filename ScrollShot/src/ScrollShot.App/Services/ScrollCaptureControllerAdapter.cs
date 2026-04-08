using ScrollShot.Capture.Models;
using ScrollShot.Scroll;
using ScrollShot.Scroll.Models;

namespace ScrollShot.App.Services;

internal sealed class ScrollCaptureControllerAdapter : IScrollCaptureController
{
    private readonly CaptureController _controller;

    public ScrollCaptureControllerAdapter(CaptureController controller)
    {
        _controller = controller;
    }

    public void Start(ScreenRect region, ScrollDirection direction)
    {
        _controller.Start(region, direction);
    }

    public Task<bool> CaptureAsync(CancellationToken cancellationToken = default)
    {
        return _controller.CaptureAsync(cancellationToken);
    }

    public CaptureResult Finish()
    {
        return _controller.Finish();
    }

    public void Dispose()
    {
        _controller.Dispose();
    }
}
