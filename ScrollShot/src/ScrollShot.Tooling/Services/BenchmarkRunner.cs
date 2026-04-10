using ScrollShot.StitchingData.Models;
using ScrollShot.Tooling.Models;

namespace ScrollShot.Tooling.Services;

public sealed class BenchmarkRunner
{
    private readonly DatasetReplayer _datasetReplayer;
    private readonly SyntheticDatasetGenerator _syntheticDatasetGenerator;

    public BenchmarkRunner(DatasetReplayer? datasetReplayer = null, SyntheticDatasetGenerator? syntheticDatasetGenerator = null)
    {
        _datasetReplayer = datasetReplayer ?? new DatasetReplayer();
        _syntheticDatasetGenerator = syntheticDatasetGenerator ?? new SyntheticDatasetGenerator();
    }

    public BenchmarkSuiteReport Run(BenchmarkCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var suite = BenchmarkSuiteStore.Load(options.SuitePath);
        ValidateSuite(suite);

        var suiteDirectory = Path.GetDirectoryName(options.SuitePath)
                             ?? throw new InvalidOperationException("The suite path must include a directory.");
        var outputDirectory = string.IsNullOrWhiteSpace(options.OutputDirectory)
            ? Path.Combine(suiteDirectory, "runs", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"))
            : options.OutputDirectory;
        Directory.CreateDirectory(outputDirectory);

        var datasetReports = new List<BenchmarkDatasetReport>(suite.Datasets.Count);
        foreach (var dataset in suite.Datasets)
        {
            datasetReports.Add(RunDatasetBenchmark(suite, dataset, suiteDirectory, outputDirectory));
        }

        var report = new BenchmarkSuiteReport
        {
            Name = suite.Name,
            ProfileName = suite.ProfileName,
            WarmupIterations = suite.WarmupIterations,
            MeasuredIterations = suite.MeasuredIterations,
            OutputDirectory = outputDirectory,
            Datasets = datasetReports,
        };

        BenchmarkSuiteStore.SaveReport(report, Path.Combine(outputDirectory, "summary.json"));
        return report;
    }

    private BenchmarkDatasetReport RunDatasetBenchmark(
        BenchmarkSuiteDefinition suite,
        BenchmarkDatasetDefinition dataset,
        string suiteDirectory,
        string outputDirectory)
    {
        var manifestPath = ResolveManifestPath(suite, dataset, suiteDirectory);
        var verificationDirectory = Path.Combine(outputDirectory, "verification", SanitizePathSegment(dataset.Name));
        var verificationReport = _datasetReplayer.Replay(new ReplayCommandOptions
        {
            ManifestPath = manifestPath,
            OutputDirectory = verificationDirectory,
            ProfileName = suite.ProfileName,
            PersistOutputImage = true,
            PersistReplayReport = true,
        });

        var iterations = new List<ReplayReport>(suite.MeasuredIterations);
        var scratchDirectory = Path.Combine(outputDirectory, "_scratch", SanitizePathSegment(dataset.Name));
        for (var iteration = 0; iteration < suite.WarmupIterations; iteration++)
        {
            _datasetReplayer.Replay(new ReplayCommandOptions
            {
                ManifestPath = manifestPath,
                OutputDirectory = scratchDirectory,
                ProfileName = suite.ProfileName,
                PersistOutputImage = false,
                PersistReplayReport = false,
            });
        }

        for (var iteration = 0; iteration < suite.MeasuredIterations; iteration++)
        {
            iterations.Add(_datasetReplayer.Replay(new ReplayCommandOptions
            {
                ManifestPath = manifestPath,
                OutputDirectory = scratchDirectory,
                ProfileName = suite.ProfileName,
                PersistOutputImage = false,
                PersistReplayReport = false,
            }));
        }

        return new BenchmarkDatasetReport
        {
            Name = dataset.Name,
            ManifestPath = manifestPath,
            VerificationReport = verificationReport,
            Iterations = iterations,
            ReplayElapsedMilliseconds = Summarize(iterations.Select(report => report.ReplayElapsedMilliseconds)),
            FrameLoadElapsedMilliseconds = Summarize(iterations.Select(report => report.FrameLoadElapsedMilliseconds)),
            StitchElapsedMilliseconds = Summarize(iterations.Select(report => report.StitchElapsedMilliseconds)),
            ComposeElapsedMilliseconds = Summarize(iterations.Select(report => report.ComposeElapsedMilliseconds)),
        };
    }

    private string ResolveManifestPath(BenchmarkSuiteDefinition suite, BenchmarkDatasetDefinition dataset, string suiteDirectory)
    {
        var hasManifest = !string.IsNullOrWhiteSpace(dataset.ManifestPath);
        var hasSynthetic = dataset.Synthetic is not null;
        if (hasManifest == hasSynthetic)
        {
            throw new InvalidOperationException($"Dataset '{dataset.Name}' must define exactly one of ManifestPath or Synthetic.");
        }

        if (hasManifest)
        {
            return Path.GetFullPath(Path.Combine(suiteDirectory, dataset.ManifestPath!));
        }

        var generatedRoot = Path.GetFullPath(Path.Combine(suiteDirectory, suite.GeneratedDatasetsDirectory));
        var outputDirectory = Path.Combine(generatedRoot, SanitizePathSegment(dataset.Name));
        var synthetic = dataset.Synthetic!;
        _syntheticDatasetGenerator.Generate(new SyntheticCommandOptions
        {
            OutputDirectory = outputDirectory,
            DatasetName = synthetic.DatasetName ?? dataset.Name,
            Width = synthetic.Width,
            TotalHeight = synthetic.TotalHeight,
            ViewportHeight = synthetic.ViewportHeight,
            StepPixels = synthetic.StepPixels,
            OverlapPixels = synthetic.OverlapPixels,
            FixedTop = synthetic.FixedTop,
            FixedBottom = synthetic.FixedBottom,
            FrameOrder = synthetic.FrameOrder,
        });

        return Path.Combine(outputDirectory, "manifest.json");
    }

    private static BenchmarkTimingSummary Summarize(IEnumerable<long> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
        {
            return new BenchmarkTimingSummary();
        }

        var midpoint = ordered.Length / 2;
        var median = ordered.Length % 2 == 0
            ? (ordered[midpoint - 1] + ordered[midpoint]) / 2d
            : ordered[midpoint];

        return new BenchmarkTimingSummary
        {
            Minimum = ordered[0],
            Mean = ordered.Average(),
            Median = median,
            Maximum = ordered[^1],
        };
    }

    private static void ValidateSuite(BenchmarkSuiteDefinition suite)
    {
        if (string.IsNullOrWhiteSpace(suite.Name))
        {
            throw new InvalidOperationException("Benchmark suite name is required.");
        }

        if (suite.WarmupIterations < 0)
        {
            throw new InvalidOperationException("WarmupIterations cannot be negative.");
        }

        if (suite.MeasuredIterations <= 0)
        {
            throw new InvalidOperationException("MeasuredIterations must be positive.");
        }

        if (suite.Datasets.Count == 0)
        {
            throw new InvalidOperationException("Benchmark suite must include at least one dataset.");
        }
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "dataset" : sanitized;
    }
}
