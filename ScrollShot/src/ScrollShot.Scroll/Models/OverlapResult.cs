namespace ScrollShot.Scroll.Models;

public readonly record struct OverlapResult(int OverlapPixels, bool IsIdentical, double Confidence)
{
    public static OverlapResult NoMatch => new(0, false, 0);

    public static OverlapResult Identical(double confidence = 1.0) => new(0, true, confidence);
}
