namespace ScrollShot.Tooling.Models;

public sealed class StitchDatasetFrame
{
    public int Index { get; init; }

    public string RelativePath { get; init; } = string.Empty;

    public int OffsetPixels { get; init; }

    public int? ExpectedOverlapWithPreviousPixels { get; init; }
}
