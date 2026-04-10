using System.Drawing;
using System.Drawing.Imaging;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;
using ScrollShot.Scroll.Profiles.Current;
using ScrollShot.Scroll.Shared;

namespace ScrollShot.Scroll.Profiles.Bidirectional;

public sealed class BidirectionalScrollSession : IScrollSession
{
    private readonly IZoneDetector _zoneDetector;
    private readonly IBidirectionalOverlapMatcher _overlapMatcher;
    private readonly List<CapturedFrame> _frameHistory = new();
    private readonly List<PlacedBand> _placements = new();
    private readonly List<NormalizedPlacedBand> _normalizedPlacements = new();
    private ZoneLayout? _zoneLayout;
    private Bitmap? _fixedTopBitmap;
    private Bitmap? _fixedBottomBitmap;
    private Bitmap? _fixedLeftBitmap;
    private Bitmap? _fixedRightBitmap;
    private ScreenRect _region;
    private ScrollDirection _direction;
    private int _primaryAxisExtent;
    private int _compositionStartFrameIndex;
    private int _processedFrameCount;
    private PixelBufferSnapshot? _previousBandSnapshot;
    private int _previousPlacementStart;
    private int _previousPrimarySize;
    private bool _started;
    private bool _finished;

    public BidirectionalScrollSession(IZoneDetector? zoneDetector = null, IBidirectionalOverlapMatcher? overlapMatcher = null)
    {
        _zoneDetector = zoneDetector ?? new ZoneDetector();
        _overlapMatcher = overlapMatcher ?? new BidirectionalOverlapMatcher();
    }

    public event Action<ScrollSegment>? SegmentAdded;

    public event Action<ZoneLayout>? ZonesDetected;

    public event Action<Bitmap>? PreviewUpdated;

    public void Start(ScreenRect region, ScrollDirection direction)
    {
        Reset();
        _region = region;
        _direction = direction;
        _started = true;
    }

    public void ProcessFrame(CapturedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (!_started)
        {
            throw new InvalidOperationException("The session must be started before processing frames.");
        }

        if (_finished)
        {
            throw new InvalidOperationException("The session has already been finished.");
        }

        _frameHistory.Add(CloneFrame(frame));
        if (_frameHistory.Count < 2 || !TryEstimateZoneFromHistory(out var estimatedZone, out var startFrameIndex))
        {
            return;
        }

        var previousZone = _zoneLayout;
        var previousPlacementBitmaps = SegmentAdded is null
            ? null
            : _placements.Select(placement => placement.Bitmap).ToHashSet(ReferenceEqualityComparer.Instance);
        if (CanAppendIncrementally(estimatedZone, startFrameIndex))
        {
            AppendLatestFrameToComposition(_frameHistory[^1].Bitmap, estimatedZone);
        }
        else
        {
            RebuildFromHistory(estimatedZone, startFrameIndex);
        }

        if (previousZone != estimatedZone)
        {
            ZonesDetected?.Invoke(estimatedZone);
        }

        if (SegmentAdded is not null && previousPlacementBitmaps is not null)
        {
            foreach (var segment in _normalizedPlacements.Where(segment => !previousPlacementBitmaps.Contains(segment.Bitmap)))
            {
                SegmentAdded(new ScrollSegment((Bitmap)segment.Bitmap.Clone(), segment.Offset));
            }
        }

        RaisePreviewUpdated();
    }

    public void Finish()
    {
        if (!_started)
        {
            throw new InvalidOperationException("The session must be started before it can be finished.");
        }

        _finished = true;
    }

    public CaptureResult GetResult()
    {
        if (!_finished)
        {
            throw new InvalidOperationException("Finish must be called before retrieving the result.");
        }

        if (_zoneLayout is null || _normalizedPlacements.Count == 0)
        {
            throw new InvalidOperationException("At least two frames are required to build a scroll result.");
        }

        var totalWidth = _direction == ScrollDirection.Vertical
            ? _zoneLayout.Value.FixedLeft + _zoneLayout.Value.ScrollBand.Width + _zoneLayout.Value.FixedRight
            : _zoneLayout.Value.FixedLeft + _primaryAxisExtent + _zoneLayout.Value.FixedRight;
        var totalHeight = _direction == ScrollDirection.Vertical
            ? _zoneLayout.Value.FixedTop + _primaryAxisExtent + _zoneLayout.Value.FixedBottom
            : _zoneLayout.Value.FixedTop + _zoneLayout.Value.ScrollBand.Height + _zoneLayout.Value.FixedBottom;

        return new CaptureResult(
            _normalizedPlacements.Select(segment => new ScrollSegment((Bitmap)segment.Bitmap.Clone(), segment.Offset)).ToArray(),
            _zoneLayout.Value,
            _direction,
            totalWidth,
            totalHeight,
            fixedTopBitmap: _fixedTopBitmap is null ? null : (Bitmap)_fixedTopBitmap.Clone(),
            fixedBottomBitmap: _fixedBottomBitmap is null ? null : (Bitmap)_fixedBottomBitmap.Clone(),
            fixedLeftBitmap: _fixedLeftBitmap is null ? null : (Bitmap)_fixedLeftBitmap.Clone(),
            fixedRightBitmap: _fixedRightBitmap is null ? null : (Bitmap)_fixedRightBitmap.Clone());
    }

    public void Dispose()
    {
        Reset();
    }

    private bool TryEstimateZoneFromHistory(out ZoneLayout zoneLayout, out int startFrameIndex)
    {
        var candidates = new List<(int StartIndex, ZoneLayout Zone)>();
        for (var index = 1; index < _frameHistory.Count; index++)
        {
            var previous = _frameHistory[index - 1];
            var current = _frameHistory[index];
            var detectedZone = _zoneDetector.DetectZones(previous, current, _direction);
            using var previousBand = ExtractBandBitmap(previous.Bitmap, detectedZone.ScrollBand);
            var previousBandSnapshot = PixelBuffer.FromBitmap(previousBand);
            using var currentBand = ExtractBandBitmap(current.Bitmap, detectedZone.ScrollBand);
            var currentBandSnapshot = PixelBuffer.FromBitmap(currentBand);
            var overlap = FindOverlap(previousBandSnapshot, currentBandSnapshot);
            if (overlap.HasMatch)
            {
                candidates.Add((index - 1, detectedZone));
            }
        }

        if (candidates.Count == 0)
        {
            zoneLayout = default;
            startFrameIndex = 0;
            return false;
        }

        startFrameIndex = candidates.Min(candidate => candidate.StartIndex);
        zoneLayout = AggregateZones(candidates.Select(candidate => candidate.Zone));
        return true;
    }

    private ZoneLayout AggregateZones(IEnumerable<ZoneLayout> zones)
    {
        var zoneList = zones.ToList();
        var fixedTop = Median(zoneList.Select(zone => zone.FixedTop));
        var fixedBottom = Median(zoneList.Select(zone => zone.FixedBottom));
        var fixedLeft = Median(zoneList.Select(zone => zone.FixedLeft));
        var fixedRight = Median(zoneList.Select(zone => zone.FixedRight));

        if (fixedTop + fixedBottom >= _region.Height)
        {
            fixedTop = 0;
            fixedBottom = 0;
        }

        if (fixedLeft + fixedRight >= _region.Width)
        {
            fixedLeft = 0;
            fixedRight = 0;
        }

        return new ZoneLayout(
            fixedTop,
            fixedBottom,
            fixedLeft,
            fixedRight,
            new ScreenRect(
                fixedLeft,
                fixedTop,
                _region.Width - fixedLeft - fixedRight,
                _region.Height - fixedTop - fixedBottom));
    }

    private static int Median(IEnumerable<int> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
        {
            return 0;
        }

        return ordered[ordered.Length / 2];
    }

    private bool CanAppendIncrementally(ZoneLayout zoneLayout, int startFrameIndex)
    {
        return _zoneLayout == zoneLayout &&
               _compositionStartFrameIndex == startFrameIndex &&
               _processedFrameCount == _frameHistory.Count - 1 &&
               _placements.Count > 0 &&
               _previousBandSnapshot is not null;
    }

    private void RebuildFromHistory(ZoneLayout zoneLayout, int startFrameIndex)
    {
        ClearComposition();
        _zoneLayout = zoneLayout;
        _compositionStartFrameIndex = startFrameIndex;
        CaptureFixedRegions(_frameHistory[startFrameIndex].Bitmap, zoneLayout);

        using var startBandBitmap = ExtractBandBitmap(_frameHistory[startFrameIndex].Bitmap, zoneLayout.ScrollBand);
        AddInitialPlacement(startBandBitmap);
        _processedFrameCount = startFrameIndex + 1;

        for (var index = startFrameIndex + 1; index < _frameHistory.Count; index++)
        {
            ProcessFrameIntoComposition(_frameHistory[index].Bitmap, zoneLayout);
            _processedFrameCount = index + 1;
        }

        RefreshNormalizedPlacements();
    }

    private void AppendLatestFrameToComposition(Bitmap bitmap, ZoneLayout zoneLayout)
    {
        ProcessFrameIntoComposition(bitmap, zoneLayout);
        _processedFrameCount = _frameHistory.Count;
        RefreshNormalizedPlacements();
    }

    private void AddInitialPlacement(Bitmap startBandBitmap)
    {
        _placements.Add(new PlacedBand((Bitmap)startBandBitmap.Clone(), 0));
        _previousBandSnapshot = PixelBuffer.FromBitmap(startBandBitmap);
        _previousPlacementStart = 0;
        _previousPrimarySize = GetPrimaryAxisSize(startBandBitmap);
    }

    private void ProcessFrameIntoComposition(Bitmap currentBitmap, ZoneLayout zoneLayout)
    {
        if (_previousBandSnapshot is null)
        {
            return;
        }

        using var currentBandBitmap = ExtractBandBitmap(currentBitmap, zoneLayout.ScrollBand);
        var currentSnapshot = PixelBuffer.FromBitmap(currentBandBitmap);
        var overlap = FindOverlap(_previousBandSnapshot.Value, currentSnapshot);
        if (!overlap.HasMatch)
        {
            return;
        }

        if (overlap.IsIdentical)
        {
            _previousBandSnapshot = currentSnapshot;
            _previousPrimarySize = GetPrimaryAxisSize(currentBandBitmap);
            return;
        }

        var currentPrimarySize = GetPrimaryAxisSize(currentBandBitmap);
        var currentPlacementStart = overlap.Placement == ScrollPlacement.AppendAfter
            ? _previousPlacementStart + _previousPrimarySize - overlap.OverlapPixels
            : _previousPlacementStart - (currentPrimarySize - overlap.OverlapPixels);

        _placements.Add(new PlacedBand((Bitmap)currentBandBitmap.Clone(), currentPlacementStart));
        _previousBandSnapshot = currentSnapshot;
        _previousPlacementStart = currentPlacementStart;
        _previousPrimarySize = currentPrimarySize;
    }

    private void RefreshNormalizedPlacements()
    {
        _normalizedPlacements.Clear();
        if (_placements.Count == 0)
        {
            _primaryAxisExtent = 0;
            return;
        }

        var minOffset = _placements.Min(placement => placement.Offset);
        var normalizedPlacements = _placements
            .Select(placement => new NormalizedPlacedBand(placement.Bitmap, placement.Offset - minOffset))
            .OrderBy(placement => placement.Offset);

        foreach (var placement in normalizedPlacements)
        {
            _normalizedPlacements.Add(placement);
        }

        _primaryAxisExtent = _normalizedPlacements.Max(placement => placement.Offset + GetPrimaryAxisSize(placement.Bitmap));
    }

    private void CaptureFixedRegions(Bitmap bitmap, ZoneLayout zoneLayout)
    {
        DisposeFixedRegions();

        if (zoneLayout.FixedTop > 0)
        {
            _fixedTopBitmap = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, zoneLayout.FixedTop), PixelFormat.Format32bppArgb);
        }

        if (zoneLayout.FixedBottom > 0)
        {
            _fixedBottomBitmap = bitmap.Clone(
                new Rectangle(0, bitmap.Height - zoneLayout.FixedBottom, bitmap.Width, zoneLayout.FixedBottom),
                PixelFormat.Format32bppArgb);
        }

        if (zoneLayout.FixedLeft > 0)
        {
            _fixedLeftBitmap = bitmap.Clone(
                new Rectangle(0, zoneLayout.ScrollBand.Y, zoneLayout.FixedLeft, zoneLayout.ScrollBand.Height),
                PixelFormat.Format32bppArgb);
        }

        if (zoneLayout.FixedRight > 0)
        {
            _fixedRightBitmap = bitmap.Clone(
                new Rectangle(bitmap.Width - zoneLayout.FixedRight, zoneLayout.ScrollBand.Y, zoneLayout.FixedRight, zoneLayout.ScrollBand.Height),
                PixelFormat.Format32bppArgb);
        }
    }

    private void RaisePreviewUpdated()
    {
        if (PreviewUpdated is null || _normalizedPlacements.Count == 0 || _zoneLayout is null)
        {
            return;
        }

        var previewWidth = _direction == ScrollDirection.Vertical
            ? _zoneLayout.Value.ScrollBand.Width
            : _normalizedPlacements.Max(segment => segment.Offset + segment.Bitmap.Width);
        var previewHeight = _direction == ScrollDirection.Vertical
            ? _normalizedPlacements.Max(segment => segment.Offset + segment.Bitmap.Height)
            : _zoneLayout.Value.ScrollBand.Height;

        var previewBitmap = new Bitmap(previewWidth, previewHeight, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(previewBitmap))
        {
            graphics.Clear(Color.Transparent);
            foreach (var segment in _normalizedPlacements)
            {
                DrawBitmapPixelExact(
                    graphics,
                    segment.Bitmap,
                    _direction == ScrollDirection.Vertical ? 0 : segment.Offset,
                    _direction == ScrollDirection.Vertical ? segment.Offset : 0);
            }
        }

        var targetSize = _direction == ScrollDirection.Vertical
            ? new Size(Math.Max(1, Math.Min(180, previewBitmap.Width)), Math.Max(1, Math.Min(320, previewBitmap.Height)))
            : new Size(Math.Max(1, Math.Min(320, previewBitmap.Width)), Math.Max(1, Math.Min(180, previewBitmap.Height)));

        if (targetSize.Width != previewBitmap.Width || targetSize.Height != previewBitmap.Height)
        {
            using var original = previewBitmap;
            PreviewUpdated(PixelBuffer.Downscale(original, targetSize));
            return;
        }

        PreviewUpdated(previewBitmap);
    }

    private static void DrawBitmapPixelExact(Graphics graphics, Bitmap bitmap, int x, int y)
    {
        graphics.DrawImage(
            bitmap,
            new Rectangle(x, y, bitmap.Width, bitmap.Height),
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            GraphicsUnit.Pixel);
    }

    private Bitmap ExtractBandBitmap(Bitmap bitmap, ScreenRect band)
    {
        return bitmap.Clone(new Rectangle(band.X, band.Y, band.Width, band.Height), PixelFormat.Format32bppArgb);
    }

    private DirectionalOverlapResult FindOverlap(PixelBufferSnapshot previousBand, PixelBufferSnapshot currentBand)
    {
        return _overlapMatcher.FindOverlap(
            previousBand.Pixels,
            currentBand.Pixels,
            currentBand.Width,
            currentBand.Height,
            _direction);
    }

    private CapturedFrame CloneFrame(CapturedFrame frame)
    {
        return new CapturedFrame((Bitmap)frame.Bitmap.Clone(), frame.Region, frame.CapturedAtUtc, frame.DpiScale);
    }

    private int GetPrimaryAxisSize(Bitmap bitmap)
    {
        return _direction == ScrollDirection.Vertical ? bitmap.Height : bitmap.Width;
    }

    private void DisposeFixedRegions()
    {
        _fixedTopBitmap?.Dispose();
        _fixedBottomBitmap?.Dispose();
        _fixedLeftBitmap?.Dispose();
        _fixedRightBitmap?.Dispose();
        _fixedTopBitmap = null;
        _fixedBottomBitmap = null;
        _fixedLeftBitmap = null;
        _fixedRightBitmap = null;
    }

    private void ClearComposition()
    {
        _primaryAxisExtent = 0;
        _compositionStartFrameIndex = -1;
        _processedFrameCount = 0;
        _previousBandSnapshot = null;
        _previousPlacementStart = 0;
        _previousPrimarySize = 0;

        foreach (var placement in _placements)
        {
            placement.Bitmap.Dispose();
        }

        _placements.Clear();
        _normalizedPlacements.Clear();
        DisposeFixedRegions();
    }

    private void Reset()
    {
        foreach (var frame in _frameHistory)
        {
            frame.Dispose();
        }

        _frameHistory.Clear();
        ClearComposition();
        _zoneLayout = null;
        _started = false;
        _finished = false;
    }

    private sealed record PlacedBand(Bitmap Bitmap, int Offset);

    private sealed record NormalizedPlacedBand(Bitmap Bitmap, int Offset);
}
