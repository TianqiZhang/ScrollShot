using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ScrollShot.Editor.Services;

public sealed class ImageFileService : IImageFileService
{
    public void SavePng(Bitmap bitmap, string path)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        bitmap.Save(path, ImageFormat.Png);
    }
}
