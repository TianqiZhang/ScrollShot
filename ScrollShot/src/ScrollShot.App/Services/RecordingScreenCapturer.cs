using System.Globalization;
using ScrollShot.Capture;
using ScrollShot.Capture.Models;

namespace ScrollShot.App.Services;

internal sealed class RecordingScreenCapturer : IScreenCapturer
{
    private readonly IScreenCapturer _inner;
    private readonly ScrollCaptureDebugDumpSession _debugDumpSession;

    public RecordingScreenCapturer(IScreenCapturer inner, ScrollCaptureDebugDumpSession debugDumpSession)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _debugDumpSession = debugDumpSession ?? throw new ArgumentNullException(nameof(debugDumpSession));
    }

    public bool IsAvailable => _inner.IsAvailable;

    public void Initialize(ScreenRect region)
    {
        _inner.Initialize(region);
    }

    public CapturedFrame? CaptureFrame()
    {
        var frame = _inner.CaptureFrame();
        if (frame is null)
        {
            return null;
        }

        _debugDumpSession.RecordFrame(
            frame,
            $"frame-captured dpi={frame.DpiScale.ToString("F2", CultureInfo.InvariantCulture)}");
        return frame;
    }

    public void Dispose()
    {
        _inner.Dispose();
    }
}
