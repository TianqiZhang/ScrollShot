using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScrollShot.Scroll.Shared;

public readonly record struct PixelBufferSnapshot(int Width, int Height, int Stride, byte[] Pixels);

public static class PixelBuffer
{
    public const int BytesPerPixel = 4;

    public static PixelBufferSnapshot FromBitmap(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bitmapData = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            var stride = Math.Abs(bitmapData.Stride);
            var pixels = new byte[stride * bitmap.Height];
            Marshal.Copy(bitmapData.Scan0, pixels, 0, pixels.Length);
            return new PixelBufferSnapshot(bitmap.Width, bitmap.Height, stride, pixels);
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }
    }

    public static Bitmap ToBitmap(PixelBufferSnapshot snapshot)
    {
        var bitmap = new Bitmap(snapshot.Width, snapshot.Height, PixelFormat.Format32bppArgb);
        var rectangle = new Rectangle(0, 0, snapshot.Width, snapshot.Height);
        var bitmapData = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        try
        {
            Marshal.Copy(snapshot.Pixels, 0, bitmapData.Scan0, snapshot.Pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return bitmap;
    }

    public static PixelBufferSnapshot ExtractSubRectangle(PixelBufferSnapshot source, Rectangle rectangle)
    {
        if (rectangle.X < 0 ||
            rectangle.Y < 0 ||
            rectangle.Right > source.Width ||
            rectangle.Bottom > source.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(rectangle));
        }

        var targetStride = rectangle.Width * BytesPerPixel;
        var pixels = new byte[targetStride * rectangle.Height];

        for (var row = 0; row < rectangle.Height; row++)
        {
            var sourceOffset = ((rectangle.Y + row) * source.Stride) + (rectangle.X * BytesPerPixel);
            var targetOffset = row * targetStride;
            Buffer.BlockCopy(source.Pixels, sourceOffset, pixels, targetOffset, targetStride);
        }

        return new PixelBufferSnapshot(rectangle.Width, rectangle.Height, targetStride, pixels);
    }

    public static long ComputeSumOfAbsoluteDifferences(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException("Buffers must have the same length.");
        }

        long sum = 0;
        for (var index = 0; index < left.Length; index++)
        {
            sum += Math.Abs(left[index] - right[index]);
        }

        return sum;
    }

    public static double ComputeNormalizedDifference(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length == 0)
        {
            return 0;
        }

        var sad = ComputeSumOfAbsoluteDifferences(left, right);
        return sad / (255d * left.Length);
    }

    public static double ComputeRowDifference(PixelBufferSnapshot previous, PixelBufferSnapshot current, int row)
    {
        var rowLength = previous.Width * BytesPerPixel;
        var previousOffset = row * previous.Stride;
        var currentOffset = row * current.Stride;
        return ComputeNormalizedDifference(
            previous.Pixels.AsSpan(previousOffset, rowLength),
            current.Pixels.AsSpan(currentOffset, rowLength));
    }

    public static double ComputeColumnDifference(
        PixelBufferSnapshot previous,
        PixelBufferSnapshot current,
        int column,
        int startRow,
        int rowCount)
    {
        var differences = 0d;
        var byteOffset = column * BytesPerPixel;

        for (var row = 0; row < rowCount; row++)
        {
            var previousOffset = ((startRow + row) * previous.Stride) + byteOffset;
            var currentOffset = ((startRow + row) * current.Stride) + byteOffset;
            differences += ComputeNormalizedDifference(
                previous.Pixels.AsSpan(previousOffset, BytesPerPixel),
                current.Pixels.AsSpan(currentOffset, BytesPerPixel));
        }

        return differences / rowCount;
    }

    public static Bitmap Downscale(Bitmap source, Size targetSize)
    {
        ArgumentNullException.ThrowIfNull(source);

        var bitmap = new Bitmap(targetSize.Width, targetSize.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, new Rectangle(Point.Empty, targetSize));
        return bitmap;
    }
}
