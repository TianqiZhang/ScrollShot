using ScrollShot.Capture.Models;
using ScrollShot.Scroll;
using ScrollShot.Scroll.Models;

namespace ScrollShot.App.Services;

internal sealed class ScrollCaptureControllerAdapter : IScrollCaptureController
{
    private readonly CaptureController _controller;
    private readonly Func<Task>? _beforeCaptureAsync;
    private readonly Func<Task>? _afterCaptureAsync;

    public ScrollCaptureControllerAdapter(
        CaptureController controller,
        Func<Task>? beforeCaptureAsync = null,
        Func<Task>? afterCaptureAsync = null)
    {
        _controller = controller;
        _beforeCaptureAsync = beforeCaptureAsync;
        _afterCaptureAsync = afterCaptureAsync;
    }

    public void Start(ScreenRect region, ScrollDirection direction)
    {
        _controller.Start(region, direction);
    }

    public async Task<bool> CaptureAsync(CancellationToken cancellationToken = default)
    {
        if (_beforeCaptureAsync is not null)
        {
            await _beforeCaptureAsync();
        }

        try
        {
            return await _controller.CaptureAsync(cancellationToken);
        }
        finally
        {
            if (_afterCaptureAsync is not null)
            {
                await _afterCaptureAsync();
            }
        }
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
