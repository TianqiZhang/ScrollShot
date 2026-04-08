using System.Drawing;

namespace ScrollShot.Editor.Services;

public interface IImageFileService
{
    void SavePng(Bitmap bitmap, string path);
}
