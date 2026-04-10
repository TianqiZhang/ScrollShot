using ScrollShot.Scroll.Models;

namespace ScrollShot.StitchingData.Models;

public sealed record ReplayReport
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public string DatasetName { get; init; } = string.Empty;

    public string ProfileName { get; init; } = string.Empty;

    public int FrameCount { get; init; }

    public int SegmentCount { get; init; }

    public int OutputWidth { get; init; }

    public int OutputHeight { get; init; }

    public string? OutputImageRelativePath { get; init; }

    public double? NormalizedDifferenceToGroundTruth { get; init; }

    public bool? GroundTruthDimensionsMatch { get; init; }

    public long ReplayElapsedMilliseconds { get; init; }

    public long FrameLoadElapsedMilliseconds { get; init; }

    public long StitchElapsedMilliseconds { get; init; }

    public long ComposeElapsedMilliseconds { get; init; }

    public ScrollDirection Direction { get; init; }
}
