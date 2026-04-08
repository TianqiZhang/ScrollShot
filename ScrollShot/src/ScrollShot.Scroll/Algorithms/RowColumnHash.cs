namespace ScrollShot.Scroll.Algorithms;

public static class RowColumnHash
{
    public static long[] ComputeRowHashes(ReadOnlySpan<byte> pixels, int width, int height, int stride)
    {
        var hashes = new long[height];
        var rowLength = width * PixelBuffer.BytesPerPixel;

        for (var row = 0; row < height; row++)
        {
            long sum = 0;
            var rowSpan = pixels.Slice(row * stride, rowLength);
            foreach (var value in rowSpan)
            {
                sum += value;
            }

            hashes[row] = sum;
        }

        return hashes;
    }

    public static long[] ComputeColumnHashes(ReadOnlySpan<byte> pixels, int width, int height, int stride)
    {
        var hashes = new long[width];

        for (var column = 0; column < width; column++)
        {
            long sum = 0;
            var columnOffset = column * PixelBuffer.BytesPerPixel;

            for (var row = 0; row < height; row++)
            {
                var pixelOffset = (row * stride) + columnOffset;
                for (var channel = 0; channel < PixelBuffer.BytesPerPixel; channel++)
                {
                    sum += pixels[pixelOffset + channel];
                }
            }

            hashes[column] = sum;
        }

        return hashes;
    }

    public static double RowDifference(ReadOnlySpan<byte> rowA, ReadOnlySpan<byte> rowB)
    {
        return PixelBuffer.ComputeNormalizedDifference(rowA, rowB);
    }
}
