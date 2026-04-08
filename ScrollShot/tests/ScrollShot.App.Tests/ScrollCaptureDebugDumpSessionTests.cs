using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using FluentAssertions;
using ScrollShot.App.Services;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;
using ScrollShot.StitchingData.Services;

namespace ScrollShot.App.Tests;

public sealed class ScrollCaptureDebugDumpSessionTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"ScrollShot.App.Tests.{Guid.NewGuid():N}");

    public ScrollCaptureDebugDumpSessionTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Complete_SavesManifestFramesAndReport()
    {
        var session = ScrollCaptureDebugDumpSession.Create(
            _tempDirectory,
            new ScreenRect(10, 20, 3, 4),
            ScrollDirection.Vertical,
            TimeSpan.FromMilliseconds(120));

        using var firstFrame = CreateFrame(3, 4, DateTimeOffset.UtcNow);
        using var secondFrame = CreateFrame(3, 4, DateTimeOffset.UtcNow.AddMilliseconds(120));
        session.RecordFrame(firstFrame, "first");
        session.RecordFrame(secondFrame, "second");

        using var segmentBitmap = CreateFilledBitmap(3, 6, Color.CadetBlue);
        var result = new CaptureResult(
            new[] { new ScrollSegment((Bitmap)segmentBitmap.Clone(), 0) },
            new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 6)),
            ScrollDirection.Vertical,
            3,
            6,
            isScrollingCapture: true);

        try
        {
            session.Complete(result);
        }
        finally
        {
            foreach (var segment in result.Segments)
            {
                segment.Dispose();
            }
        }

        var manifest = ManifestStore.Load(Path.Combine(session.OutputDirectory, "manifest.json"));
        manifest.Source.Should().Be("live-capture");
        manifest.CompletionReason.Should().Be("completed");
        manifest.SamplingIntervalMilliseconds.Should().Be(120);
        manifest.CaptureRegion.Should().NotBeNull();
        manifest.CaptureRegion!.X.Should().Be(10);
        manifest.CaptureRegion.Y.Should().Be(20);
        manifest.Frames.Should().HaveCount(2);
        manifest.Frames[0].Trace.Should().Be("first");
        manifest.Frames[1].Trace.Should().Be("second");
        manifest.Frames[0].CapturedAtUtc.Should().NotBeNull();
        manifest.Frames[1].ElapsedMilliseconds.Should().BeGreaterThan(0);
        File.Exists(Path.Combine(session.OutputDirectory, "frames", "frame_0000.png")).Should().BeTrue();
        File.Exists(Path.Combine(session.OutputDirectory, "stitched.png")).Should().BeTrue();
        File.Exists(Path.Combine(session.OutputDirectory, "report.json")).Should().BeTrue();
    }

    [Fact]
    public void Cancel_SavesManifestWithoutReplayArtifacts()
    {
        var session = ScrollCaptureDebugDumpSession.Create(
            _tempDirectory,
            new ScreenRect(5, 6, 3, 4),
            ScrollDirection.Vertical,
            TimeSpan.FromMilliseconds(120));

        using var frame = CreateFrame(3, 4, DateTimeOffset.UtcNow);
        session.RecordFrame(frame, "captured");
        session.Cancel("cancelled");

        var manifest = ManifestStore.Load(Path.Combine(session.OutputDirectory, "manifest.json"));
        manifest.CompletionReason.Should().Be("cancelled");
        manifest.Frames.Should().ContainSingle();
        File.Exists(Path.Combine(session.OutputDirectory, "stitched.png")).Should().BeFalse();
        File.Exists(Path.Combine(session.OutputDirectory, "report.json")).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static CapturedFrame CreateFrame(int width, int height, DateTimeOffset capturedAtUtc)
    {
        return new CapturedFrame(CreateFilledBitmap(width, height, Color.DarkSlateBlue), new ScreenRect(0, 0, width, height), capturedAtUtc);
    }

    private static Bitmap CreateFilledBitmap(int width, int height, Color color)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }
}
