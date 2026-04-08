using ScrollShot.Capture.Models;

namespace ScrollShot.Capture;

public interface IScreenCapturer : IDisposable
{
    void Initialize(ScreenRect region);

    CapturedFrame? CaptureFrame();

    bool IsAvailable { get; }
}
