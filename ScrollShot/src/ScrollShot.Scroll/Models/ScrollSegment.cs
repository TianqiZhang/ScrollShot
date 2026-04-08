using System.Drawing;

namespace ScrollShot.Scroll.Models;

public sealed class ScrollSegment : IDisposable
{
    public ScrollSegment(Bitmap bitmap, int offset, string? temporaryFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        Bitmap = bitmap;
        Offset = offset;
        TemporaryFilePath = temporaryFilePath;
    }

    public Bitmap Bitmap { get; }

    public int Offset { get; }

    public string? TemporaryFilePath { get; }

    public bool IsPersistedToDisk => !string.IsNullOrWhiteSpace(TemporaryFilePath);

    public void Dispose()
    {
        Bitmap.Dispose();
    }
}
