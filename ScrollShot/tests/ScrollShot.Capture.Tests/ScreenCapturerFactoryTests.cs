using FluentAssertions;
using ScrollShot.Capture.Models;

namespace ScrollShot.Capture.Tests;

public sealed class ScreenCapturerFactoryTests
{
    [Fact]
    public void Create_UsesPrimaryCapturer_WhenAvailable()
    {
        var primary = new FakeCapturer(isAvailable: true);
        var fallback = new FakeCapturer(isAvailable: true);

        var capturer = ScreenCapturerFactory.Create(
            new ScreenRect(0, 0, 10, 10),
            () => primary,
            () => fallback);

        capturer.Should().BeSameAs(primary);
        primary.InitializedRegion.Should().Be(new ScreenRect(0, 0, 10, 10));
        fallback.InitializedRegion.Should().BeNull();
    }

    [Fact]
    public void Create_FallsBack_WhenPrimaryIsUnavailable()
    {
        var primary = new FakeCapturer(isAvailable: false);
        var fallback = new FakeCapturer(isAvailable: true);

        var capturer = ScreenCapturerFactory.Create(
            new ScreenRect(1, 2, 30, 40),
            () => primary,
            () => fallback);

        capturer.Should().BeSameAs(fallback);
        primary.IsDisposed.Should().BeTrue();
        fallback.InitializedRegion.Should().Be(new ScreenRect(1, 2, 30, 40));
    }

    private sealed class FakeCapturer : IScreenCapturer
    {
        public FakeCapturer(bool isAvailable)
        {
            IsAvailable = isAvailable;
        }

        public bool IsAvailable { get; }

        public bool IsDisposed { get; private set; }

        public ScreenRect? InitializedRegion { get; private set; }

        public CapturedFrame? CaptureFrame() => null;

        public void Initialize(ScreenRect region)
        {
            InitializedRegion = region;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
