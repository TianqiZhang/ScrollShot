using System.Drawing;

namespace ScrollShot.Capture.Models;

public sealed class CapturedFrame : IDisposable
{
    public CapturedFrame(Bitmap bitmap, ScreenRect region, DateTimeOffset capturedAtUtc, float dpiScale = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        if (dpiScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpiScale), "DPI scale must be greater than zero.");
        }

        Bitmap = bitmap;
        Region = region;
        CapturedAtUtc = capturedAtUtc;
        DpiScale = dpiScale;
    }

    public Bitmap Bitmap { get; }

    public ScreenRect Region { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public float DpiScale { get; }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        Bitmap.Dispose();
        IsDisposed = true;
    }
}
