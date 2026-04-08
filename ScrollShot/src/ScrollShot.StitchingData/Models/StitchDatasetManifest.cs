using ScrollShot.Scroll.Models;

namespace ScrollShot.StitchingData.Models;

public sealed class StitchDatasetManifest
{
    public int Version { get; init; } = 1;

    public string Name { get; init; } = string.Empty;

    public string Source { get; init; } = "synthetic";

    public ScrollDirection Direction { get; init; } = ScrollDirection.Vertical;

    public int ViewportWidth { get; init; }

    public int ViewportHeight { get; init; }

    public int StepPixels { get; init; }

    public StitchCaptureRegion? CaptureRegion { get; init; }

    public int? SamplingIntervalMilliseconds { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public string? CompletionReason { get; init; }

    public string? FailureMessage { get; init; }

    public StitchDatasetTruth? Truth { get; init; }

    public IReadOnlyList<StitchDatasetFrame> Frames { get; init; } = Array.Empty<StitchDatasetFrame>();
}
