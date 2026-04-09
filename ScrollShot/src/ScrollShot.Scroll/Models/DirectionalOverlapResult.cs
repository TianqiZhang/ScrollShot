namespace ScrollShot.Scroll.Models;

public readonly record struct DirectionalOverlapResult(int OverlapPixels, bool IsIdentical, double Confidence, ScrollPlacement Placement)
{
    public static DirectionalOverlapResult NoMatch(ScrollPlacement placement = ScrollPlacement.AppendAfter) => new(0, false, 0, placement);

    public static DirectionalOverlapResult Identical(double confidence = 1.0, ScrollPlacement placement = ScrollPlacement.AppendAfter) => new(0, true, confidence, placement);

    public bool HasMatch => IsIdentical || OverlapPixels > 0;
}
