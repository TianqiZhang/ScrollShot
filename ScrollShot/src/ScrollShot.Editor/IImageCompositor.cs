using System.Drawing;
using ScrollShot.Editor.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Editor;

public interface IImageCompositor
{
    Bitmap Compose(CaptureResult result, EditState editState);
}
