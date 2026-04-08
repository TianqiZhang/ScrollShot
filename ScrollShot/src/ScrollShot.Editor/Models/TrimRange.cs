namespace ScrollShot.Editor.Models;

public readonly record struct TrimRange
{
    public TrimRange(int headTrimPixels, int tailTrimPixels)
    {
        if (headTrimPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(headTrimPixels));
        }

        if (tailTrimPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tailTrimPixels));
        }

        HeadTrimPixels = headTrimPixels;
        TailTrimPixels = tailTrimPixels;
    }

    public int HeadTrimPixels { get; }

    public int TailTrimPixels { get; }
}
