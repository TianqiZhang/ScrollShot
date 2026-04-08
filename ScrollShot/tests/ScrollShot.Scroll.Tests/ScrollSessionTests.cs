using System.Drawing;
using FluentAssertions;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll.Tests;

public sealed class ScrollSessionTests
{
    [Fact]
    public void ProcessFrame_EmitsSegmentsAndZones()
    {
        var detector = new FakeZoneDetector(new ZoneLayout(1, 1, 0, 0, new ScreenRect(0, 1, 4, 4)));
        var matcher = new SequenceOverlapMatcher(new OverlapResult(2, false, 1));
        using var session = new ScrollSession(detector, matcher);
        var segments = new List<ScrollSegment>();
        ZoneLayout? detectedZone = null;
        session.SegmentAdded += segment => segments.Add(segment);
        session.ZonesDetected += zone => detectedZone = zone;

        session.Start(new ScreenRect(0, 0, 4, 6), ScrollDirection.Vertical);
        using var frameOne = CreateCapturedFrame(Color.Red, 4, 6);
        using var frameTwo = CreateCapturedFrame(Color.Blue, 4, 6);
        session.ProcessFrame(frameOne);
        session.ProcessFrame(frameTwo);
        session.Finish();

        var result = session.GetResult();

        detectedZone.Should().Be(detector.Layout);
        segments.Should().HaveCount(2);
        result.Segments.Should().HaveCount(2);
        result.TotalHeight.Should().Be(1 + 4 + 2 + 1);
    }

    [Fact]
    public void ProcessFrame_SkipsIdenticalFrames()
    {
        var detector = new FakeZoneDetector(new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 3)));
        var matcher = new SequenceOverlapMatcher(OverlapResult.Identical());
        using var session = new ScrollSession(detector, matcher);

        session.Start(new ScreenRect(0, 0, 3, 3), ScrollDirection.Vertical);
        using var frameOne = CreateCapturedFrame(Color.Red, 3, 3);
        using var frameTwo = CreateCapturedFrame(Color.Red, 3, 3);
        session.ProcessFrame(frameOne);
        session.ProcessFrame(frameTwo);
        session.Finish();

        session.GetResult().Segments.Should().ContainSingle();
    }

    [Fact]
    public async Task CaptureController_CapturesAndProcessesFrames()
    {
        var frames = new Queue<CapturedFrame?>(new[]
        {
            CreateCapturedFrame(Color.Red, 3, 3),
            CreateCapturedFrame(Color.Blue, 3, 3),
        });

        using var controller = new CaptureController(
            new FakeCapturer(frames),
            new ScrollSession(
                new FakeZoneDetector(new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 3))),
                new SequenceOverlapMatcher(new OverlapResult(1, false, 1))));

        controller.Start(new ScreenRect(0, 0, 3, 3), ScrollDirection.Vertical);

        (await controller.CaptureAsync()).Should().BeTrue();
        (await controller.CaptureAsync()).Should().BeTrue();

        var result = controller.Finish();
        result.Segments.Should().HaveCount(2);
    }

    private static CapturedFrame CreateCapturedFrame(Color color, int width, int height)
    {
        return new CapturedFrame(CreateFrame(color, width, height), new ScreenRect(0, 0, width, height), DateTimeOffset.UtcNow);
    }

    private static Bitmap CreateFrame(Color color, int width = 4, int height = 6)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }

    private sealed class FakeZoneDetector : IZoneDetector
    {
        public FakeZoneDetector(ZoneLayout layout)
        {
            Layout = layout;
        }

        public ZoneLayout Layout { get; }

        public ZoneLayout DetectZones(CapturedFrame previous, CapturedFrame current, ScrollDirection direction) => Layout;

        public ZoneLayout RefineZones(ZoneLayout existing, CapturedFrame previous, CapturedFrame current, ScrollDirection direction) => Layout;
    }

    private sealed class SequenceOverlapMatcher : IOverlapMatcher
    {
        private readonly Queue<OverlapResult> _results;

        public SequenceOverlapMatcher(params OverlapResult[] results)
        {
            _results = new Queue<OverlapResult>(results);
        }

        public OverlapResult FindOverlap(ReadOnlySpan<byte> previousBand, ReadOnlySpan<byte> currentBand, int width, int height, ScrollDirection direction)
        {
            return _results.Count > 0 ? _results.Dequeue() : OverlapResult.NoMatch;
        }
    }

    private sealed class FakeCapturer : Capture.IScreenCapturer
    {
        private readonly Queue<CapturedFrame?> _frames;

        public FakeCapturer(Queue<CapturedFrame?> frames)
        {
            _frames = frames;
        }

        public bool IsAvailable => true;

        public void Initialize(ScreenRect region)
        {
        }

        public CapturedFrame? CaptureFrame()
        {
            return _frames.Dequeue();
        }

        public void Dispose()
        {
            while (_frames.Count > 0)
            {
                _frames.Dequeue()?.Dispose();
            }
        }
    }
}
