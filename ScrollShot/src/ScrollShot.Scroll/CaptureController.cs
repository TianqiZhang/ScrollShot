using ScrollShot.Capture;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll;

public sealed class CaptureController : IDisposable
{
    private readonly IScreenCapturer _capturer;
    private readonly IScrollSession _scrollSession;
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private bool _started;

    public CaptureController(IScreenCapturer capturer, IScrollSession scrollSession)
    {
        _capturer = capturer ?? throw new ArgumentNullException(nameof(capturer));
        _scrollSession = scrollSession ?? throw new ArgumentNullException(nameof(scrollSession));
    }

    public void Start(ScreenRect region, ScrollDirection direction)
    {
        _capturer.Initialize(region);
        _scrollSession.Start(region, direction);
        _started = true;
    }

    public async Task<bool> CaptureAsync(CancellationToken cancellationToken = default)
    {
        if (!_started)
        {
            throw new InvalidOperationException("Capture must be started before frames can be requested.");
        }

        await _captureLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var frame = await Task.Run(_capturer.CaptureFrame, cancellationToken).ConfigureAwait(false);
            if (frame is null)
            {
                return false;
            }

            using (frame)
            {
                _scrollSession.ProcessFrame(frame);
            }

            return true;
        }
        finally
        {
            _captureLock.Release();
        }
    }

    public CaptureResult Finish()
    {
        _scrollSession.Finish();
        return _scrollSession.GetResult();
    }

    public void Dispose()
    {
        _captureLock.Dispose();
        _scrollSession.Dispose();
        _capturer.Dispose();
    }
}
