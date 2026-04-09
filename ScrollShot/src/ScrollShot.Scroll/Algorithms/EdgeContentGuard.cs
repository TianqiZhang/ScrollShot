namespace ScrollShot.Scroll.Algorithms;

internal static class EdgeContentGuard
{
    public static bool HasRichVerticalContent(
        PixelBufferSnapshot snapshot,
        int startColumn,
        int columnCount,
        int startRow,
        int rowCount,
        double threshold)
    {
        if (columnCount <= 0 || rowCount <= 1)
        {
            return false;
        }

        long totalDifference = 0;
        var comparisons = 0;

        for (var y = startRow + 1; y < startRow + rowCount; y++)
        {
            for (var x = startColumn; x < startColumn + columnCount; x++)
            {
                var currentIndex = ((y * snapshot.Width) + x) * 4;
                var previousIndex = ((((y - 1) * snapshot.Width) + x) * 4);
                totalDifference += Math.Abs(snapshot.Pixels[currentIndex] - snapshot.Pixels[previousIndex]);
                totalDifference += Math.Abs(snapshot.Pixels[currentIndex + 1] - snapshot.Pixels[previousIndex + 1]);
                totalDifference += Math.Abs(snapshot.Pixels[currentIndex + 2] - snapshot.Pixels[previousIndex + 2]);
                comparisons += 3;
            }
        }

        if (comparisons == 0)
        {
            return false;
        }

        var averageDifference = totalDifference / (comparisons * 255d);
        return averageDifference >= threshold;
    }
}
