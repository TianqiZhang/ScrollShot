using ScrollShot.Scroll;

namespace ScrollShot.Tooling.Models;

public sealed class BenchmarkSuiteDefinition
{
    public string Name { get; init; } = "bidirectional-performance";

    public string ProfileName { get; init; } = StitchingProfiles.BidirectionalCurrentExperiment;

    public int WarmupIterations { get; init; } = 1;

    public int MeasuredIterations { get; init; } = 5;

    public string GeneratedDatasetsDirectory { get; init; } = "generated-datasets";

    public IReadOnlyList<BenchmarkDatasetDefinition> Datasets { get; init; } = Array.Empty<BenchmarkDatasetDefinition>();
}

public sealed class BenchmarkDatasetDefinition
{
    public string Name { get; init; } = string.Empty;

    public string? ManifestPath { get; init; }

    public BenchmarkSyntheticDatasetDefinition? Synthetic { get; init; }
}

public sealed class BenchmarkSyntheticDatasetDefinition
{
    public string? DatasetName { get; init; }

    public int Width { get; init; } = 480;

    public int TotalHeight { get; init; } = 2200;

    public int ViewportHeight { get; init; } = 420;

    public int? StepPixels { get; init; }

    public int? OverlapPixels { get; init; }

    public int FixedTop { get; init; } = 48;

    public int FixedBottom { get; init; } = 32;

    public SyntheticFrameOrder FrameOrder { get; init; } = SyntheticFrameOrder.Forward;
}
