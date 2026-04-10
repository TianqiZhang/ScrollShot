using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Numerics;

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

        if (!Vector.IsHardwareAccelerated || left.Length < Vector<byte>.Count)
        {
            return ComputeSumOfAbsoluteDifferencesScalar(left, right);
        }

        var vectorLength = Vector<byte>.Count;
        var lastVectorStart = left.Length - vectorLength;
        var index = 0;
        var sum0 = Vector<ulong>.Zero;
        var sum1 = Vector<ulong>.Zero;
        var sum2 = Vector<ulong>.Zero;
        var sum3 = Vector<ulong>.Zero;
        var sum4 = Vector<ulong>.Zero;
        var sum5 = Vector<ulong>.Zero;
        var sum6 = Vector<ulong>.Zero;
        var sum7 = Vector<ulong>.Zero;

        while (index <= lastVectorStart)
        {
            var leftVector = new Vector<byte>(left.Slice(index, vectorLength));
            var rightVector = new Vector<byte>(right.Slice(index, vectorLength));

            Vector.Widen(leftVector, out Vector<ushort> leftLow, out Vector<ushort> leftHigh);
            Vector.Widen(rightVector, out Vector<ushort> rightLow, out Vector<ushort> rightHigh);

            var differenceLow = Vector.Max(leftLow, rightLow) - Vector.Min(leftLow, rightLow);
            var differenceHigh = Vector.Max(leftHigh, rightHigh) - Vector.Min(leftHigh, rightHigh);

            Vector.Widen(differenceLow, out Vector<uint> difference0, out Vector<uint> difference1);
            Vector.Widen(differenceHigh, out Vector<uint> difference2, out Vector<uint> difference3);

            AccumulateDifference(difference0, ref sum0, ref sum1);
            AccumulateDifference(difference1, ref sum2, ref sum3);
            AccumulateDifference(difference2, ref sum4, ref sum5);
            AccumulateDifference(difference3, ref sum6, ref sum7);

            index += vectorLength;
        }

        ulong sum =
            SumVector(sum0) +
            SumVector(sum1) +
            SumVector(sum2) +
            SumVector(sum3) +
            SumVector(sum4) +
            SumVector(sum5) +
            SumVector(sum6) +
            SumVector(sum7);

        if (index < left.Length)
        {
            sum += (ulong)ComputeSumOfAbsoluteDifferencesScalar(left.Slice(index), right.Slice(index));
        }

        return (long)sum;
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

    private static long ComputeSumOfAbsoluteDifferencesScalar(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        long sum = 0;
        for (var index = 0; index < left.Length; index++)
        {
            sum += Math.Abs(left[index] - right[index]);
        }

        return sum;
    }

    private static void AccumulateDifference(Vector<uint> difference, ref Vector<ulong> lowAccumulator, ref Vector<ulong> highAccumulator)
    {
        Vector.Widen(difference, out Vector<ulong> low, out Vector<ulong> high);
        lowAccumulator += low;
        highAccumulator += high;
    }

    private static ulong SumVector(Vector<ulong> vector)
    {
        ulong sum = 0;
        for (var index = 0; index < Vector<ulong>.Count; index++)
        {
            sum += vector[index];
        }

        return sum;
    }
}
