using System.Drawing;
using FluentAssertions;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;
using ScrollShot.Scroll.Profiles.Bidirectional;

namespace ScrollShot.Scroll.Tests;

public sealed class BidirectionalScrollSessionTests
{
    [Fact]
    public void UpwardOverlap_NormalizesPrependIntoEarlierOffset()
    {
        var detector = new FixedZoneDetector(new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 4)));
        var matcher = new SequenceBidirectionalOverlapMatcher(
            new DirectionalOverlapResult(3, false, 1, ScrollPlacement.PrependBefore),
            new DirectionalOverlapResult(3, false, 1, ScrollPlacement.PrependBefore));
        using var session = new BidirectionalScrollSession(detector, matcher);

        session.Start(new ScreenRect(0, 0, 3, 4), ScrollDirection.Vertical);
        using var firstFrame = CreateCapturedFrame(Color.Red, 3, 4);
        using var secondFrame = CreateCapturedFrame(Color.Blue, 3, 4);
        session.ProcessFrame(firstFrame);
        session.ProcessFrame(secondFrame);
        session.Finish();

        var result = session.GetResult();

        result.TotalHeight.Should().Be(5);
        result.Segments.Should().HaveCount(2);
        result.Segments[0].Offset.Should().Be(0);
        result.Segments[1].Offset.Should().Be(1);
    }

    [Fact]
    public void DirectionChangeWithinSession_CanAppendThenPrepend()
    {
        var detector = new FixedZoneDetector(new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 4)));
        var matcher = new SequenceBidirectionalOverlapMatcher(
            new DirectionalOverlapResult(3, false, 1, ScrollPlacement.AppendAfter),
            new DirectionalOverlapResult(2, false, 1, ScrollPlacement.PrependBefore));
        using var session = new BidirectionalScrollSession(detector, matcher);
        var eventOffsets = new List<int>();
        session.SegmentAdded += segment => eventOffsets.Add(segment.Offset);

        session.Start(new ScreenRect(0, 0, 3, 4), ScrollDirection.Vertical);
        using var firstFrame = CreateCapturedFrame(Color.Red, 3, 4);
        using var secondFrame = CreateCapturedFrame(Color.Blue, 3, 4);
        using var thirdFrame = CreateCapturedFrame(Color.Green, 3, 4);
        session.ProcessFrame(firstFrame);
        session.ProcessFrame(secondFrame);
        session.ProcessFrame(thirdFrame);
        session.Finish();

        var result = session.GetResult();

        result.TotalHeight.Should().Be(6);
        result.Segments.Select(segment => segment.Offset).Should().Equal(0, 1, 2);
        eventOffsets.Should().Equal(0, 1, 0);
    }

    [Fact]
    public void IdenticalIntermediateFrame_CanStillAppendLaterFrame()
    {
        var detector = new FixedZoneDetector(new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 4)));
        var matcher = new SequenceBidirectionalOverlapMatcher(
            DirectionalOverlapResult.Identical(),
            new DirectionalOverlapResult(3, false, 1, ScrollPlacement.AppendAfter));
        using var session = new BidirectionalScrollSession(detector, matcher);

        session.Start(new ScreenRect(0, 0, 3, 4), ScrollDirection.Vertical);
        using var firstFrame = CreateCapturedFrame(Color.Red, 3, 4);
        using var secondFrame = CreateCapturedFrame(Color.Red, 3, 4);
        using var thirdFrame = CreateCapturedFrame(Color.Blue, 3, 4);
        session.ProcessFrame(firstFrame);
        session.ProcessFrame(secondFrame);
        session.ProcessFrame(thirdFrame);
        session.Finish();

        var result = session.GetResult();

        result.TotalHeight.Should().Be(5);
        result.Segments.Should().HaveCount(2);
        result.Segments.Select(segment => segment.Offset).Should().Equal(0, 1);
    }

    [Fact]
    public void AdjacentPairAnalysis_IsCachedAcrossHistoryRescans()
    {
        var detector = new CountingZoneDetector(new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 4)));
        var matcher = new CountingBidirectionalOverlapMatcher(new DirectionalOverlapResult(3, false, 1, ScrollPlacement.AppendAfter));
        using var session = new BidirectionalScrollSession(detector, matcher);

        session.Start(new ScreenRect(0, 0, 3, 4), ScrollDirection.Vertical);
        using var firstFrame = CreateCapturedFrame(Color.Red, 3, 4);
        using var secondFrame = CreateCapturedFrame(Color.Blue, 3, 4);
        using var thirdFrame = CreateCapturedFrame(Color.Green, 3, 4);
        using var fourthFrame = CreateCapturedFrame(Color.Yellow, 3, 4);
        session.ProcessFrame(firstFrame);
        session.ProcessFrame(secondFrame);
        session.ProcessFrame(thirdFrame);
        session.ProcessFrame(fourthFrame);
        session.Finish();

        var result = session.GetResult();

        detector.CallCount.Should().Be(3);
        matcher.CallCount.Should().Be(6);
        result.TotalHeight.Should().Be(7);
        result.Segments.Select(segment => segment.Offset).Should().Equal(0, 1, 2, 3);
    }

    private static CapturedFrame CreateCapturedFrame(Color color, int width, int height)
    {
        return new CapturedFrame(CreateFrame(color, width, height), new ScreenRect(0, 0, width, height), DateTimeOffset.UtcNow);
    }

    private static Bitmap CreateFrame(Color color, int width, int height)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }

    private sealed class FixedZoneDetector : IZoneDetector
    {
        private readonly ZoneLayout _layout;

        public FixedZoneDetector(ZoneLayout layout)
        {
            _layout = layout;
        }

        public ZoneLayout DetectZones(CapturedFrame previous, CapturedFrame current, ScrollDirection direction) => _layout;
    }

    private sealed class CountingZoneDetector : IZoneDetector
    {
        private readonly ZoneLayout _layout;

        public CountingZoneDetector(ZoneLayout layout)
        {
            _layout = layout;
        }

        public int CallCount { get; private set; }

        public ZoneLayout DetectZones(CapturedFrame previous, CapturedFrame current, ScrollDirection direction)
        {
            CallCount++;
            return _layout;
        }
    }

    private sealed class SequenceBidirectionalOverlapMatcher : IBidirectionalOverlapMatcher
    {
        private readonly Queue<DirectionalOverlapResult> _results;
        private readonly Dictionary<string, DirectionalOverlapResult> _memoizedResults = new();

        public SequenceBidirectionalOverlapMatcher(params DirectionalOverlapResult[] results)
        {
            _results = new Queue<DirectionalOverlapResult>(results);
        }

        public DirectionalOverlapResult FindOverlap(ReadOnlySpan<byte> previousBand, ReadOnlySpan<byte> currentBand, int width, int height, ScrollDirection direction)
        {
            var key = $"{direction}:{width}x{height}:{ComputeFingerprint(previousBand)}:{ComputeFingerprint(currentBand)}";
            if (_memoizedResults.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var next = _results.Count > 0 ? _results.Dequeue() : DirectionalOverlapResult.NoMatch();
            _memoizedResults[key] = next;
            return next;
        }

        private static ulong ComputeFingerprint(ReadOnlySpan<byte> pixels)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            foreach (var value in pixels)
            {
                hash ^= value;
                hash *= prime;
            }

            return hash;
        }
    }

    private sealed class CountingBidirectionalOverlapMatcher : IBidirectionalOverlapMatcher
    {
        private readonly DirectionalOverlapResult _result;

        public CountingBidirectionalOverlapMatcher(DirectionalOverlapResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public DirectionalOverlapResult FindOverlap(ReadOnlySpan<byte> previousBand, ReadOnlySpan<byte> currentBand, int width, int height, ScrollDirection direction)
        {
            CallCount++;
            return _result;
        }
    }
}
