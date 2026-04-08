using ScrollShot.Scroll.Models;

namespace ScrollShot.Tooling.Models;

public sealed class StitchDatasetManifest
{
    public int Version { get; init; } = 1;

    public string Name { get; init; } = string.Empty;

    public ScrollDirection Direction { get; init; } = ScrollDirection.Vertical;

    public int ViewportWidth { get; init; }

    public int ViewportHeight { get; init; }

    public int StepPixels { get; init; }

    public StitchDatasetTruth? Truth { get; init; }

    public IReadOnlyList<StitchDatasetFrame> Frames { get; init; } = Array.Empty<StitchDatasetFrame>();
}
