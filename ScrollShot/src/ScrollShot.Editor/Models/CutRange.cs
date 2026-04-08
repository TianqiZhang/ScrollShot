namespace ScrollShot.Editor.Models;

public readonly record struct CutRange
{
    public CutRange(int startPixel, int endPixel)
    {
        if (startPixel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startPixel));
        }

        if (endPixel <= startPixel)
        {
            throw new ArgumentOutOfRangeException(nameof(endPixel), "End pixel must be greater than start pixel.");
        }

        StartPixel = startPixel;
        EndPixel = endPixel;
    }

    public int StartPixel { get; }

    public int EndPixel { get; }
}
