using System.Drawing;
using FluentAssertions;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll;
using ScrollShot.Scroll.Profiles.Current;
using ScrollShot.Scroll.Shared;
using ScrollShot.Scroll.Models;
using ScrollShot.Tooling.Models;
using ScrollShot.Tooling.Services;

namespace ScrollShot.Tooling.Tests;

public sealed class SyntheticDatasetGeneratorTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"ScrollShot.Tooling.Tests.{Guid.NewGuid():N}");

    public SyntheticDatasetGeneratorTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Generate_CreatesSyntheticDatasetWithGroundTruth()
    {
        var outputDirectory = Path.Combine(_tempDirectory, "synthetic-dataset");
        var generator = new SyntheticDatasetGenerator();

        var manifest = generator.Generate(new SyntheticCommandOptions
        {
            OutputDirectory = outputDirectory,
            DatasetName = "synthetic-basic",
            Width = 420,
            TotalHeight = 1600,
            ViewportHeight = 320,
            StepPixels = 160,
            FixedTop = 48,
            FixedBottom = 28,
        });

        manifest.Name.Should().Be("synthetic-basic");
        manifest.Source.Should().Be("synthetic");
        manifest.Frames.Should().HaveCountGreaterThan(2);
        File.Exists(Path.Combine(outputDirectory, "manifest.json")).Should().BeTrue();
        File.Exists(Path.Combine(outputDirectory, "groundtruth.png")).Should().BeTrue();
    }

    [Fact]
    public void GeneratedDataset_BasicDetectionAndReplayWorkOnSyntheticScene()
    {
        var outputDirectory = Path.Combine(_tempDirectory, "synthetic-scene");
        var generator = new SyntheticDatasetGenerator();
        var manifest = generator.Generate(new SyntheticCommandOptions
        {
            OutputDirectory = outputDirectory,
            DatasetName = "synthetic-detection",
            Width = 440,
            TotalHeight = 1800,
            ViewportHeight = 360,
            StepPixels = 180,
            FixedTop = 52,
            FixedBottom = 30,
        });

        var framePath0 = Path.Combine(outputDirectory, manifest.Frames[0].RelativePath);
        var framePath1 = Path.Combine(outputDirectory, manifest.Frames[1].RelativePath);
        using var previousBitmap = LoadBitmap(framePath0);
        using var currentBitmap = LoadBitmap(framePath1);
        using var previous = new CapturedFrame(previousBitmap, new ScreenRect(0, 0, previousBitmap.Width, previousBitmap.Height), DateTimeOffset.UtcNow);
        using var current = new CapturedFrame(currentBitmap, new ScreenRect(0, 0, currentBitmap.Width, currentBitmap.Height), DateTimeOffset.UtcNow);

        var zoneDetector = new ZoneDetector();
        var zone = zoneDetector.DetectZones(previous, current, ScrollDirection.Vertical);
        zone.FixedTop.Should().Be(52);
        zone.FixedBottom.Should().Be(30);

        using var previousBandBitmap = previousBitmap.Clone(new Rectangle(zone.ScrollBand.X, zone.ScrollBand.Y, zone.ScrollBand.Width, zone.ScrollBand.Height), previousBitmap.PixelFormat);
        using var currentBandBitmap = currentBitmap.Clone(new Rectangle(zone.ScrollBand.X, zone.ScrollBand.Y, zone.ScrollBand.Width, zone.ScrollBand.Height), currentBitmap.PixelFormat);
        var previousBand = PixelBuffer.FromBitmap(previousBandBitmap);
        var currentBand = PixelBuffer.FromBitmap(currentBandBitmap);
        var overlap = new OverlapMatcher().FindOverlap(previousBand.Pixels, currentBand.Pixels, previousBand.Width, previousBand.Height, ScrollDirection.Vertical);
        overlap.OverlapPixels.Should().Be(98);

        var replay = new DatasetReplayer().Replay(new ReplayCommandOptions
        {
            ManifestPath = Path.Combine(outputDirectory, "manifest.json"),
            OutputDirectory = Path.Combine(outputDirectory, "replay"),
            ProfileName = StitchingProfiles.Current,
        });

        replay.Succeeded.Should().BeTrue();
        replay.GroundTruthDimensionsMatch.Should().BeTrue();
        replay.NormalizedDifferenceToGroundTruth.Should().HaveValue().And.BeLessThan(0.0001);
    }

    [Fact]
    public void Generate_CanReverseFrameOrderForUpwardProgressionFixtures()
    {
        var outputDirectory = Path.Combine(_tempDirectory, "synthetic-up");
        var manifest = new SyntheticDatasetGenerator().Generate(new SyntheticCommandOptions
        {
            OutputDirectory = outputDirectory,
            DatasetName = "synthetic-up",
            Width = 320,
            TotalHeight = 1400,
            ViewportHeight = 360,
            StepPixels = 180,
            FixedTop = 48,
            FixedBottom = 32,
            FrameOrder = SyntheticFrameOrder.Reverse,
        });

        manifest.Frames.Should().HaveCountGreaterThan(2);
        manifest.CompletionReason.Should().Be("generated-reverse");
        manifest.Frames[0].OffsetPixels.Should().BeGreaterThan(manifest.Frames[1].OffsetPixels!.Value);
        manifest.Frames[1].ExpectedOverlapWithPreviousPixels.Should().BeGreaterThan(0);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static Bitmap LoadBitmap(string path)
    {
        using var bitmap = new Bitmap(path);
        return (Bitmap)bitmap.Clone();
    }
}
