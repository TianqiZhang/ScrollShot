using System.Drawing;

namespace ScrollShot.Editor.Services;

public interface IClipboardService
{
    void SetImage(Bitmap bitmap);
}
