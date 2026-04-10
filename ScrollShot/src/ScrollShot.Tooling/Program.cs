using ScrollShot.Scroll;
using ScrollShot.Tooling.Models;
using ScrollShot.Tooling.Services;

return await ToolCli.RunAsync(args);

public static class ToolCli
{
    public static Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return Task.FromResult(1);
            }

            var command = args[0].ToLowerInvariant();
            var options = ParseOptions(args.Skip(1).ToArray());

            switch (command)
            {
                case "slice":
                    RunSlice(options);
                    return Task.FromResult(0);
                case "synthesize":
                    RunSynthesize(options);
                    return Task.FromResult(0);
                case "replay":
                    RunReplay(options);
                    return Task.FromResult(0);
                case "benchmark":
                    RunBenchmark(options);
                    return Task.FromResult(0);
                case "help":
                case "--help":
                case "-h":
                    PrintUsage();
                    return Task.FromResult(0);
                default:
                    throw new InvalidOperationException($"Unknown command '{command}'.");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FileNotFoundException)
        {
            Console.Error.WriteLine(exception.Message);
            PrintUsage();
            return Task.FromResult(1);
        }
    }

    private static void RunSlice(IReadOnlyDictionary<string, string> options)
    {
        var slicer = new LongScreenshotSlicer();
        var manifest = slicer.Slice(new SliceCommandOptions
        {
            InputImagePath = GetRequired(options, "input"),
            OutputDirectory = GetRequired(options, "output"),
            DatasetName = GetOptional(options, "name"),
            ViewportHeight = GetRequiredInt(options, "viewport-height"),
            ViewportWidth = GetOptionalInt(options, "viewport-width"),
            StepPixels = GetOptionalInt(options, "step"),
            OverlapPixels = GetOptionalInt(options, "overlap"),
            CropX = GetOptionalInt(options, "crop-x") ?? 0,
        });

        Console.WriteLine($"Dataset '{manifest.Name}' written with {manifest.Frames.Count} frames.");
    }

    private static void RunSynthesize(IReadOnlyDictionary<string, string> options)
    {
        var generator = new SyntheticDatasetGenerator();
        var manifest = generator.Generate(new SyntheticCommandOptions
        {
            OutputDirectory = GetRequired(options, "output"),
            DatasetName = GetOptional(options, "name"),
            Width = GetOptionalInt(options, "width") ?? 480,
            TotalHeight = GetOptionalInt(options, "total-height") ?? 2200,
            ViewportHeight = GetRequiredInt(options, "viewport-height"),
            StepPixels = GetOptionalInt(options, "step"),
            OverlapPixels = GetOptionalInt(options, "overlap"),
            FixedTop = GetOptionalInt(options, "fixed-top") ?? 48,
            FixedBottom = GetOptionalInt(options, "fixed-bottom") ?? 32,
            FrameOrder = GetOptionalEnum<SyntheticFrameOrder>(options, "frame-order") ?? SyntheticFrameOrder.Forward,
        });

        Console.WriteLine($"Synthetic dataset '{manifest.Name}' written with {manifest.Frames.Count} frames.");
    }

    private static void RunReplay(IReadOnlyDictionary<string, string> options)
    {
        var replayer = new DatasetReplayer();
        var report = replayer.Replay(new ReplayCommandOptions
        {
            ManifestPath = GetRequired(options, "manifest"),
            OutputDirectory = GetRequired(options, "output"),
            ProfileName = GetOptional(options, "profile") ?? StitchingProfiles.Current,
        });

        if (!report.Succeeded)
        {
            throw new InvalidOperationException(report.ErrorMessage ?? "Replay failed.");
        }

        Console.WriteLine($"Replay succeeded with {report.SegmentCount} segments.");
        if (report.NormalizedDifferenceToGroundTruth.HasValue)
        {
            Console.WriteLine($"Normalized difference to ground truth: {report.NormalizedDifferenceToGroundTruth.Value:F6}");
        }
    }

    private static void RunBenchmark(IReadOnlyDictionary<string, string> options)
    {
        var report = new BenchmarkRunner().Run(new BenchmarkCommandOptions
        {
            SuitePath = GetRequired(options, "suite"),
            OutputDirectory = GetOptional(options, "output"),
        });

        Console.WriteLine($"Benchmark suite '{report.Name}' completed for profile '{report.ProfileName}'.");
        Console.WriteLine($"Summary written to {Path.Combine(report.OutputDirectory, "summary.json")}");
        foreach (var dataset in report.Datasets)
        {
            Console.WriteLine(
                $"{dataset.Name}: stitch median {dataset.StitchElapsedMilliseconds.Median:F1} ms, " +
                $"replay median {dataset.ReplayElapsedMilliseconds.Median:F1} ms, " +
                $"ground-truth diff {dataset.VerificationReport.NormalizedDifferenceToGroundTruth?.ToString("F6") ?? "n/a"}");
        }
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var current = args[index];
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{current}'. Options must use the --name value form.");
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Option '{current}' must be followed by a value.");
            }

            options[current[2..]] = args[index + 1];
            index++;
        }

        return options;
    }

    private static string GetRequired(IReadOnlyDictionary<string, string> options, string name)
    {
        if (!options.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"The --{name} option is required.");
        }

        return value;
    }

    private static string? GetOptional(IReadOnlyDictionary<string, string> options, string name)
    {
        return options.TryGetValue(name, out var value) ? value : null;
    }

    private static int GetRequiredInt(IReadOnlyDictionary<string, string> options, string name)
    {
        var raw = GetRequired(options, name);
        if (!int.TryParse(raw, out var value))
        {
            throw new ArgumentException($"The --{name} option must be an integer.");
        }

        return value;
    }

    private static int? GetOptionalInt(IReadOnlyDictionary<string, string> options, string name)
    {
        if (!options.TryGetValue(name, out var raw))
        {
            return null;
        }

        if (!int.TryParse(raw, out var value))
        {
            throw new ArgumentException($"The --{name} option must be an integer.");
        }

        return value;
    }

    private static TEnum? GetOptionalEnum<TEnum>(IReadOnlyDictionary<string, string> options, string name)
        where TEnum : struct, Enum
    {
        if (!options.TryGetValue(name, out var raw))
        {
            return null;
        }

        if (!Enum.TryParse<TEnum>(raw, ignoreCase: true, out var value))
        {
            var allowedValues = string.Join(", ", Enum.GetNames<TEnum>());
            throw new ArgumentException($"The --{name} option must be one of: {allowedValues}.");
        }

        return value;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            ScrollShot.Tooling

            Commands:
              slice  --input <image> --output <dir> --viewport-height <px> [--viewport-width <px>] [--step <px> | --overlap <px>] [--crop-x <px>] [--name <dataset>]
              synthesize --output <dir> --viewport-height <px> [--width <px>] [--total-height <px>] [--step <px> | --overlap <px>] [--fixed-top <px>] [--fixed-bottom <px>] [--frame-order <Forward|Reverse>] [--name <dataset>]
              replay --manifest <manifest.json> --output <dir> [--profile <current|signal-zone|signal-hybrid|bidirectional-current>]
              benchmark --suite <suite.json> [--output <dir>]
            """);
    }
}
