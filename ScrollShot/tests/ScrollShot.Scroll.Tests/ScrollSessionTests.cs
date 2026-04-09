using System.Drawing;
using FluentAssertions;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll.Tests;

public sealed class ScrollSessionTests
{
    [Fact]
    public void TwoFrames_ProducesTwoSegmentsWithZoneDetection()
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

        detectedZone.Should().NotBeNull();
        segments.Should().HaveCount(2);
        result.Segments.Should().HaveCount(2);
    }

    [Fact]
    public void IdenticalFrames_AreSkipped()
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
    public void InitialNoMatchPair_DoesNotProduceResult()
    {
        var detector = new FakeZoneDetector(new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 3)));
        var matcher = new SequenceOverlapMatcher(OverlapResult.NoMatch);
        using var session = new ScrollSession(detector, matcher);

        session.Start(new ScreenRect(0, 0, 3, 3), ScrollDirection.Vertical);
        using var frameOne = CreateCapturedFrame(Color.Red, 3, 3);
        using var frameTwo = CreateCapturedFrame(Color.Blue, 3, 3);
        session.ProcessFrame(frameOne);
        session.ProcessFrame(frameTwo);
        session.Finish();

        var act = () => session.GetResult();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MultipleFrames_BuildGrowingStrip()
    {
        var detector = new FakeZoneDetector(new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 4, 4)));
        // Each frame overlaps by 2 rows, contributing 2 new rows
        var matcher = new SequenceOverlapMatcher(
            new OverlapResult(2, false, 1),
            new OverlapResult(2, false, 1),
            new OverlapResult(2, false, 1));
        using var session = new ScrollSession(detector, matcher);

        session.Start(new ScreenRect(0, 0, 4, 4), ScrollDirection.Vertical);
        for (var i = 0; i < 4; i++)
        {
            using var frame = CreateCapturedFrame(Color.FromArgb(255, i * 60, 0, 0), 4, 4);
            session.ProcessFrame(frame);
        }
        session.Finish();

        var result = session.GetResult();
        // Frame 1: initial band (4px), Frames 2-4: each add 2px new content = 4 + 2 + 2 + 2 = 10
        result.Segments.Should().HaveCount(4);
    }

    [Fact]
    public void SingleFrame_CannotProduceResult()
    {
        var detector = new FakeZoneDetector(new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 3)));
        using var session = new ScrollSession(detector);

        session.Start(new ScreenRect(0, 0, 3, 3), ScrollDirection.Vertical);
        using var frame = CreateCapturedFrame(Color.Red, 3, 3);
        session.ProcessFrame(frame);
        session.Finish();

        var act = () => session.GetResult();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PreviewUpdated_FiresWhenSegmentIsAdded()
    {
        var detector = new FakeZoneDetector(new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 3)));
        var matcher = new SequenceOverlapMatcher(new OverlapResult(1, false, 1));
        using var session = new ScrollSession(detector, matcher);
        var previewCount = 0;
        session.PreviewUpdated += _ => previewCount++;

        session.Start(new ScreenRect(0, 0, 3, 3), ScrollDirection.Vertical);
        using var f1 = CreateCapturedFrame(Color.Red, 3, 3);
        using var f2 = CreateCapturedFrame(Color.Blue, 3, 3);
        session.ProcessFrame(f1);
        session.ProcessFrame(f2);

        previewCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void InitialNoMatch_DelaysZoneInitializationUntilUsablePair()
    {
        var detector = new SequenceZoneDetector(
            new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 3)),
            new ZoneLayout(0, 1, 0, 0, new ScreenRect(0, 0, 3, 2)));
        var matcher = new SequenceOverlapMatcher(
            OverlapResult.NoMatch,
            new OverlapResult(1, false, 1),
            new OverlapResult(1, false, 1));
        using var session = new ScrollSession(detector, matcher);

        session.Start(new ScreenRect(0, 0, 3, 3), ScrollDirection.Vertical);
        using var frameOne = CreateCapturedFrame(Color.Red, 3, 3);
        using var frameTwo = CreateCapturedFrame(Color.Blue, 3, 3);
        using var frameThree = CreateCapturedFrame(Color.Green, 3, 3);
        session.ProcessFrame(frameOne);
        session.ProcessFrame(frameTwo);
        session.ProcessFrame(frameThree);
        session.Finish();

        var result = session.GetResult();
        result.ZoneLayout.FixedBottom.Should().Be(1);
        result.Segments.Should().HaveCount(2);
        detector.DetectCount.Should().Be(2);
        detector.RefineCount.Should().Be(0);
    }

    [Fact]
    public void NoMatch_RetriesZoneDetectionBeforeAppending()
    {
        var detector = new SequenceZoneDetector(
            new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 3)),
            new ZoneLayout(0, 1, 0, 0, new ScreenRect(0, 0, 3, 2)),
            new ZoneLayout(0, 1, 0, 0, new ScreenRect(0, 0, 3, 2)));
        var matcher = new SequenceOverlapMatcher(
            OverlapResult.Identical(),
            OverlapResult.NoMatch,
            new OverlapResult(1, false, 1),
            new OverlapResult(1, false, 1));
        using var session = new ScrollSession(detector, matcher);

        session.Start(new ScreenRect(0, 0, 3, 3), ScrollDirection.Vertical);
        using var frameOne = CreateCapturedFrame(Color.Red, 3, 3);
        using var frameTwo = CreateCapturedFrame(Color.Red, 3, 3);
        using var frameThree = CreateCapturedFrame(Color.Blue, 3, 3);
        session.ProcessFrame(frameOne);
        session.ProcessFrame(frameTwo);
        session.ProcessFrame(frameThree);
        session.Finish();

        var result = session.GetResult();
        result.ZoneLayout.FixedBottom.Should().Be(1);
        result.Segments.Should().HaveCount(2);
        detector.DetectCount.Should().Be(2);
        detector.RefineCount.Should().Be(1);
    }

    [Fact]
    public void LaterValidRefinement_ReplacesEarlierZoneLayout()
    {
        var initialZone = new ZoneLayout(1, 3, 0, 0, new ScreenRect(0, 1, 3, 2));
        var refinedZone = new ZoneLayout(0, 2, 0, 0, new ScreenRect(0, 0, 3, 4));
        var detector = new SequenceZoneDetector(initialZone, refinedZone);
        var matcher = new SequenceOverlapMatcher(
            new OverlapResult(2, false, 1),
            new OverlapResult(2, false, 0.8),
            new OverlapResult(2, false, 0.9));
        using var session = new ScrollSession(detector, matcher);

        session.Start(new ScreenRect(0, 0, 3, 6), ScrollDirection.Vertical);
        using var frameOne = CreateCapturedFrame(Color.Red, 3, 6);
        using var frameTwo = CreateCapturedFrame(Color.Blue, 3, 6);
        using var frameThree = CreateCapturedFrame(Color.Green, 3, 6);
        session.ProcessFrame(frameOne);
        session.ProcessFrame(frameTwo);
        session.ProcessFrame(frameThree);
        session.Finish();

        var result = session.GetResult();
        result.ZoneLayout.Should().Be(refinedZone);
    }

    [Fact]
    public void Refinement_DoesNotExpandLockedFixedRegions()
    {
        var initialZone = new ZoneLayout(0, 3, 0, 0, new ScreenRect(0, 0, 3, 3));
        var expandedZone = new ZoneLayout(0, 4, 0, 0, new ScreenRect(0, 0, 3, 2));
        var detector = new SequenceZoneDetector(initialZone, expandedZone);
        var matcher = new SequenceOverlapMatcher(
            new OverlapResult(2, false, 1),
            new OverlapResult(2, false, 0.8));
        using var session = new ScrollSession(detector, matcher);

        session.Start(new ScreenRect(0, 0, 3, 6), ScrollDirection.Vertical);
        using var frameOne = CreateCapturedFrame(Color.Red, 3, 6);
        using var frameTwo = CreateCapturedFrame(Color.Blue, 3, 6);
        using var frameThree = CreateCapturedFrame(Color.Green, 3, 6);
        session.ProcessFrame(frameOne);
        session.ProcessFrame(frameTwo);
        session.ProcessFrame(frameThree);
        session.Finish();

        var result = session.GetResult();
        result.ZoneLayout.Should().Be(initialZone);
    }

    [Fact]
    public void IdenticalTrailingFrame_DoesNotClearExistingZoneLayout()
    {
        var initialZone = new ZoneLayout(0, 3, 0, 0, new ScreenRect(0, 0, 3, 3));
        var refinedZone = new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 6));
        var detector = new SequenceZoneDetector(initialZone, refinedZone);
        var matcher = new SequenceOverlapMatcher(
            new OverlapResult(2, false, 1),
            OverlapResult.Identical(),
            OverlapResult.Identical());
        using var session = new ScrollSession(detector, matcher);

        session.Start(new ScreenRect(0, 0, 3, 6), ScrollDirection.Vertical);
        using var frameOne = CreateCapturedFrame(Color.Red, 3, 6);
        using var frameTwo = CreateCapturedFrame(Color.Blue, 3, 6);
        using var frameThree = CreateCapturedFrame(Color.Blue, 3, 6);
        session.ProcessFrame(frameOne);
        session.ProcessFrame(frameTwo);
        session.ProcessFrame(frameThree);
        session.Finish();

        var result = session.GetResult();
        result.ZoneLayout.Should().Be(initialZone);
    }

    [Fact]
    public async Task CaptureController_CapturesAndFinishesWithResult()
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

    private sealed class SequenceZoneDetector : IZoneDetector
    {
        private readonly Queue<ZoneLayout> _layouts;

        public SequenceZoneDetector(params ZoneLayout[] layouts)
        {
            _layouts = new Queue<ZoneLayout>(layouts);
        }

        public int DetectCount { get; private set; }
        public int RefineCount { get; private set; }

        public ZoneLayout DetectZones(CapturedFrame previous, CapturedFrame current, ScrollDirection direction)
        {
            DetectCount++;
            return _layouts.Count > 0 ? _layouts.Dequeue() : throw new InvalidOperationException("No queued zone layout.");
        }

        public ZoneLayout RefineZones(ZoneLayout existing, CapturedFrame previous, CapturedFrame current, ScrollDirection direction)
        {
            RefineCount++;
            return _layouts.Count > 0 ? _layouts.Dequeue() : existing;
        }
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
