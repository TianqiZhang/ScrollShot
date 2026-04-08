using FluentAssertions;
using ScrollShot.Capture.Models;
using Bitmap = System.Drawing.Bitmap;

namespace ScrollShot.Capture.Tests.Models;

public sealed class CapturedFrameTests
{
    [Fact]
    public void Constructor_SetsMetadata()
    {
        using var bitmap = new Bitmap(10, 20);
        var capturedAt = new DateTimeOffset(2026, 4, 7, 12, 0, 0, TimeSpan.Zero);

        using var frame = new CapturedFrame(bitmap, new ScreenRect(1, 2, 10, 20), capturedAt, 1.25f);

        frame.Region.Should().Be(new ScreenRect(1, 2, 10, 20));
        frame.CapturedAtUtc.Should().Be(capturedAt);
        frame.DpiScale.Should().Be(1.25f);
        frame.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Dispose_DisposesUnderlyingBitmap()
    {
        var frame = new CapturedFrame(new Bitmap(5, 5), new ScreenRect(0, 0, 5, 5), DateTimeOffset.UtcNow);

        frame.Dispose();

        frame.IsDisposed.Should().BeTrue();
    }
}
