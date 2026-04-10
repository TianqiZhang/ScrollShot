using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using ScrollShot.Scroll;
using ScrollShot.Tooling.Models;
using ScrollShot.Tooling.Services;

namespace ScrollShot.Tooling.Tests;

public sealed class BenchmarkRunnerTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"ScrollShot.Tooling.Tests.{Guid.NewGuid():N}");

    public BenchmarkRunnerTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Run_ExecutesSuiteAndWritesSummary()
    {
        var datasetDirectory = CreateIdealManifestDataset("benchmark-manifest");
        var suitePath = Path.Combine(_tempDirectory, "suite.json");
        WriteSuite(
            suitePath,
            new BenchmarkSuiteDefinition
            {
                Name = "bidirectional-performance-test",
                ProfileName = StitchingProfiles.Current,
                WarmupIterations = 1,
                MeasuredIterations = 2,
                Datasets =
                [
                    new BenchmarkDatasetDefinition
                    {
                        Name = "manifest-baseline",
                        ManifestPath = Path.GetRelativePath(Path.GetDirectoryName(suitePath)!, Path.Combine(datasetDirectory, "manifest.json")),
                    },
                    new BenchmarkDatasetDefinition
                    {
                        Name = "synthetic-up",
                        Synthetic = new BenchmarkSyntheticDatasetDefinition
                        {
                            DatasetName = "synthetic-up",
                            Width = 320,
                            TotalHeight = 1400,
                            ViewportHeight = 360,
                            StepPixels = 180,
                            FixedTop = 48,
                            FixedBottom = 32,
                            FrameOrder = SyntheticFrameOrder.Reverse,
                        },
                    },
                ],
            });

        var outputDirectory = Path.Combine(_tempDirectory, "benchmark-output");
        var report = new BenchmarkRunner().Run(new BenchmarkCommandOptions
        {
            SuitePath = suitePath,
            OutputDirectory = outputDirectory,
        });

        report.Datasets.Should().HaveCount(2);
        report.Datasets.Should().OnlyContain(dataset => dataset.Iterations.Count == 2);
        report.Datasets.Should().OnlyContain(dataset => dataset.VerificationReport.Succeeded);
        File.Exists(Path.Combine(outputDirectory, "summary.json")).Should().BeTrue();
        File.Exists(Path.Combine(outputDirectory, "verification", "manifest-baseline", "stitched.png")).Should().BeTrue();
        File.Exists(Path.Combine(outputDirectory, "verification", "synthetic-up", "stitched.png")).Should().BeTrue();
        File.Exists(Path.Combine(_tempDirectory, "generated-datasets", "synthetic-up", "manifest.json")).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private string CreateIdealManifestDataset(string datasetName)
    {
        var inputPath = Path.Combine(_tempDirectory, $"{datasetName}.png");
        using (var bitmap = CreateDistinctRowsBitmap(6, 11))
        {
            bitmap.Save(inputPath, ImageFormat.Png);
        }

        var datasetDirectory = Path.Combine(_tempDirectory, datasetName);
        new LongScreenshotSlicer().Slice(new SliceCommandOptions
        {
            InputImagePath = inputPath,
            OutputDirectory = datasetDirectory,
            ViewportHeight = 5,
            StepPixels = 3,
            DatasetName = datasetName,
        });

        return datasetDirectory;
    }

    private static void WriteSuite(string suitePath, BenchmarkSuiteDefinition suite)
    {
        var serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };
        File.WriteAllText(suitePath, JsonSerializer.Serialize(suite, serializerOptions));
    }

    private static Bitmap CreateDistinctRowsBitmap(int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, Color.FromArgb(255, (y * 17) % 255, (x * 29 + y * 7) % 255, (y * 41) % 255));
            }
        }

        return bitmap;
    }
}
