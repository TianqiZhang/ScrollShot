using ScrollShot.StitchingData.Models;

namespace ScrollShot.Tooling.Models;

public sealed class BenchmarkSuiteReport
{
    public string Name { get; init; } = string.Empty;

    public string ProfileName { get; init; } = string.Empty;

    public int WarmupIterations { get; init; }

    public int MeasuredIterations { get; init; }

    public string OutputDirectory { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<BenchmarkDatasetReport> Datasets { get; init; } = Array.Empty<BenchmarkDatasetReport>();
}

public sealed class BenchmarkDatasetReport
{
    public string Name { get; init; } = string.Empty;

    public string ManifestPath { get; init; } = string.Empty;

    public ReplayReport VerificationReport { get; init; } = new();

    public IReadOnlyList<ReplayReport> Iterations { get; init; } = Array.Empty<ReplayReport>();

    public BenchmarkTimingSummary ReplayElapsedMilliseconds { get; init; } = new();

    public BenchmarkTimingSummary FrameLoadElapsedMilliseconds { get; init; } = new();

    public BenchmarkTimingSummary StitchElapsedMilliseconds { get; init; } = new();

    public BenchmarkTimingSummary ComposeElapsedMilliseconds { get; init; } = new();
}

public sealed class BenchmarkTimingSummary
{
    public long Minimum { get; init; }

    public double Mean { get; init; }

    public double Median { get; init; }

    public long Maximum { get; init; }
}
