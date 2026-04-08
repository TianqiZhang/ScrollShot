using System.Drawing;
using System.Windows;
using ScrollShot.Editor.Helpers;

namespace ScrollShot.Editor.Services;

public sealed class ClipboardService : IClipboardService
{
    public void SetImage(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Clipboard.SetImage(BitmapSourceConversion.ToBitmapSource(bitmap));
    }
}
