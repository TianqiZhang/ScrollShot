using System.Drawing;
using System.Drawing.Imaging;
using ScrollShot.Capture.Interop;
using ScrollShot.Capture.Models;

namespace ScrollShot.Capture;

public sealed class GdiScreenCapturer : IScreenCapturer
{
    private ScreenRect? _region;

    public bool IsAvailable => true;

    public void Initialize(ScreenRect region)
    {
        _region = region;
    }

    public CapturedFrame? CaptureFrame()
    {
        if (_region is null)
        {
            throw new InvalidOperationException("The capturer must be initialized before capturing.");
        }

        var region = _region.Value;
        var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);

        using var targetGraphics = Graphics.FromImage(bitmap);
        using var screenGraphics = Graphics.FromHwnd(IntPtr.Zero);

        var targetHdc = targetGraphics.GetHdc();
        var sourceHdc = screenGraphics.GetHdc();

        try
        {
            if (!NativeMethods.BitBlt(
                    targetHdc,
                    0,
                    0,
                    region.Width,
                    region.Height,
                    sourceHdc,
                    region.X,
                    region.Y,
                    (int)(CopyPixelOperation.SourceCopy | CopyPixelOperation.CaptureBlt)))
            {
                throw new InvalidOperationException("BitBlt failed while capturing the screen.");
            }
        }
        finally
        {
            targetGraphics.ReleaseHdc(targetHdc);
            screenGraphics.ReleaseHdc(sourceHdc);
        }

        return new CapturedFrame(bitmap, region, DateTimeOffset.UtcNow);
    }

    public void Dispose()
    {
    }
}
