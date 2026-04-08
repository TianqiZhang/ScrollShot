using System.Drawing;
using System.Drawing.Imaging;
using FluentAssertions;
using ScrollShot.Scroll;
using ScrollShot.StitchingData.Models;
using ScrollShot.Tooling.Models;
using ScrollShot.Tooling.Services;

namespace ScrollShot.Tooling.Tests;

public sealed class DatasetReplayerTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"ScrollShot.Tooling.Tests.{Guid.NewGuid():N}");

    public DatasetReplayerTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Replay_ProducesStitchedOutputMatchingGroundTruthForIdealDataset()
    {
        var inputPath = Path.Combine(_tempDirectory, "groundtruth-source.png");
        using (var bitmap = CreateDistinctRowsBitmap(6, 11))
        {
            bitmap.Save(inputPath, ImageFormat.Png);
        }

        var datasetDirectory = Path.Combine(_tempDirectory, "dataset");
        var manifest = new LongScreenshotSlicer().Slice(new SliceCommandOptions
        {
            InputImagePath = inputPath,
            OutputDirectory = datasetDirectory,
            ViewportHeight = 5,
            StepPixels = 3,
            DatasetName = "ideal",
        });

        var replayDirectory = Path.Combine(_tempDirectory, "replay");
        var report = new DatasetReplayer().Replay(new ReplayCommandOptions
        {
            ManifestPath = Path.Combine(datasetDirectory, "manifest.json"),
            OutputDirectory = replayDirectory,
        });

        report.Succeeded.Should().BeTrue();
        report.FrameCount.Should().Be(manifest.Frames.Count);
        report.OutputWidth.Should().Be(6);
        report.OutputHeight.Should().Be(11);
        report.GroundTruthDimensionsMatch.Should().BeTrue();
        report.NormalizedDifferenceToGroundTruth.Should().Be(0);
        File.Exists(Path.Combine(replayDirectory, "stitched.png")).Should().BeTrue();
        File.Exists(Path.Combine(replayDirectory, "report.json")).Should().BeTrue();
    }

    [Fact]
    public void Replay_AcceptsExperimentalProfile()
    {
        var inputPath = Path.Combine(_tempDirectory, "groundtruth-experimental.png");
        using (var bitmap = CreateDistinctRowsBitmap(6, 11))
        {
            bitmap.Save(inputPath, ImageFormat.Png);
        }

        var datasetDirectory = Path.Combine(_tempDirectory, "dataset-experimental");
        new LongScreenshotSlicer().Slice(new SliceCommandOptions
        {
            InputImagePath = inputPath,
            OutputDirectory = datasetDirectory,
            ViewportHeight = 5,
            StepPixels = 3,
            DatasetName = "ideal-experimental",
        });

        var replayDirectory = Path.Combine(_tempDirectory, "replay-experimental");
        var report = new DatasetReplayer().Replay(new ReplayCommandOptions
        {
            ManifestPath = Path.Combine(datasetDirectory, "manifest.json"),
            OutputDirectory = replayDirectory,
            ProfileName = StitchingProfiles.SignalZoneExperiment,
        });

        report.Succeeded.Should().BeTrue();
        report.OutputHeight.Should().Be(11);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
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
