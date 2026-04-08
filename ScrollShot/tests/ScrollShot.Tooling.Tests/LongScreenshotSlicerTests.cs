using System.Drawing;
using System.Drawing.Imaging;
using FluentAssertions;
using ScrollShot.StitchingData.Models;
using ScrollShot.Tooling.Models;
using ScrollShot.Tooling.Services;

namespace ScrollShot.Tooling.Tests;

public sealed class LongScreenshotSlicerTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"ScrollShot.Tooling.Tests.{Guid.NewGuid():N}");

    public LongScreenshotSlicerTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Slice_CreatesManifestAndExpectedFrameOffsets()
    {
        var inputPath = Path.Combine(_tempDirectory, "long.png");
        using (var bitmap = CreateGradientBitmap(6, 11))
        {
            bitmap.Save(inputPath, ImageFormat.Png);
        }

        var outputDirectory = Path.Combine(_tempDirectory, "dataset");
        var slicer = new LongScreenshotSlicer();

        var manifest = slicer.Slice(new SliceCommandOptions
        {
            InputImagePath = inputPath,
            OutputDirectory = outputDirectory,
            ViewportHeight = 5,
            StepPixels = 3,
            DatasetName = "baseline",
        });

        manifest.Name.Should().Be("baseline");
        manifest.Source.Should().Be("synthetic");
        manifest.Frames.Select(frame => frame.OffsetPixels).Should().Equal(0, 3, 6);
        manifest.Frames.Select(frame => frame.ExpectedOverlapWithPreviousPixels).Should().Equal(null, 2, 2);
        File.Exists(Path.Combine(outputDirectory, "manifest.json")).Should().BeTrue();
        File.Exists(Path.Combine(outputDirectory, "groundtruth.png")).Should().BeTrue();
        File.Exists(Path.Combine(outputDirectory, "frames", "frame_0000.png")).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static Bitmap CreateGradientBitmap(int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, Color.FromArgb(255, (x * 30) % 255, (y * 20) % 255, (x + y) * 10 % 255));
            }
        }

        return bitmap;
    }
}
