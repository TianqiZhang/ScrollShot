namespace ScrollShot.Tooling.Models;

public sealed class StitchDatasetTruth
{
    public string GroundTruthRelativePath { get; init; } = string.Empty;

    public int GroundTruthWidth { get; init; }

    public int GroundTruthHeight { get; init; }
}
