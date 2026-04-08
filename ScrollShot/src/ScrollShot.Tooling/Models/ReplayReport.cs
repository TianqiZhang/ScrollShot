using ScrollShot.Scroll.Models;

namespace ScrollShot.Tooling.Models;

public sealed record ReplayReport
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public int FrameCount { get; init; }

    public int SegmentCount { get; init; }

    public int OutputWidth { get; init; }

    public int OutputHeight { get; init; }

    public string? OutputImageRelativePath { get; init; }

    public double? NormalizedDifferenceToGroundTruth { get; init; }

    public bool? GroundTruthDimensionsMatch { get; init; }

    public ScrollDirection Direction { get; init; }
}
