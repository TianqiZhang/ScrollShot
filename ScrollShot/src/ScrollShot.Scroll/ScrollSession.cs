using System.Drawing;
using System.Drawing.Imaging;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Algorithms;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll;

public sealed class ScrollSession : IScrollSession
{
    private readonly IZoneDetector _zoneDetector;
    private readonly IOverlapMatcher _overlapMatcher;
    private readonly List<ScrollSegment> _segments = new();
    private CapturedFrame? _previousFrame;
    private PixelBufferSnapshot? _previousBand;
    private ZoneLayout? _zoneLayout;
    private Bitmap? _fixedTopBitmap;
    private Bitmap? _fixedBottomBitmap;
    private Bitmap? _fixedLeftBitmap;
    private Bitmap? _fixedRightBitmap;
    private ScreenRect _region;
    private ScrollDirection _direction;
    private int _frameCount;
    private int _accumulatedPrimarySize;
    private bool _started;
    private bool _finished;

    public ScrollSession(IZoneDetector? zoneDetector = null, IOverlapMatcher? overlapMatcher = null)
    {
        _zoneDetector = zoneDetector ?? new ZoneDetector();
        _overlapMatcher = overlapMatcher ?? new OverlapMatcher();
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

        _frameCount++;

        if (_previousFrame is null)
        {
            _previousFrame = CloneFrame(frame);
            return;
        }

        if (_zoneLayout is null)
        {
            var detectedZone = _zoneDetector.DetectZones(_previousFrame, frame, _direction);
            var initialPreviousBand = CreateBandSnapshot(_previousFrame.Bitmap, detectedZone.ScrollBand);
            var initialOverlap = FindOverlap(initialPreviousBand, frame.Bitmap, detectedZone);
            if (!initialOverlap.IsIdentical && initialOverlap.OverlapPixels <= 0)
            {
                _previousFrame.Dispose();
                _previousFrame = CloneFrame(frame);
                return;
            }

            _zoneLayout = detectedZone;
            CaptureFixedRegions(_previousFrame.Bitmap, detectedZone);
            AppendInitialSegment(_previousFrame.Bitmap, detectedZone);
            ZonesDetected?.Invoke(detectedZone);
            AppendDelta(frame.Bitmap, detectedZone, initialOverlap);
        }
        else
        {
            var activeZone = _zoneLayout.Value;
            var activePreviousBand = _previousBand ?? CreateBandSnapshot(_previousFrame.Bitmap, activeZone.ScrollBand);
            var overlap = FindOverlap(activePreviousBand, frame.Bitmap, activeZone);
            var refinedZone = _zoneDetector.RefineZones(activeZone, _previousFrame, frame, _direction);
            var mergedZone = MergeConservativeZone(activeZone, refinedZone);
            if (mergedZone != activeZone)
            {
                var mergedPreviousBand = CreateBandSnapshot(_previousFrame.Bitmap, mergedZone.ScrollBand);
                var mergedOverlap = FindOverlap(mergedPreviousBand, frame.Bitmap, mergedZone);
                if (mergedOverlap.OverlapPixels > 0)
                {
                    activeZone = mergedZone;
                    _zoneLayout = mergedZone;
                    CaptureFixedRegions(_previousFrame.Bitmap, mergedZone);
                    _previousBand = mergedPreviousBand;
                    overlap = mergedOverlap;
                }
            }

            if (!overlap.IsIdentical && overlap.OverlapPixels <= 0)
            {
                var detectedZone = _zoneDetector.DetectZones(_previousFrame, frame, _direction);
                if (detectedZone != activeZone)
                {
                    var detectedPreviousBand = CreateBandSnapshot(_previousFrame.Bitmap, detectedZone.ScrollBand);
                    var detectedOverlap = FindOverlap(detectedPreviousBand, frame.Bitmap, detectedZone);
                    if (detectedOverlap.IsIdentical || detectedOverlap.OverlapPixels > 0)
                    {
                        activeZone = detectedZone;
                        _zoneLayout = detectedZone;
                        CaptureFixedRegions(_previousFrame.Bitmap, detectedZone);
                        _previousBand = detectedPreviousBand;
                        overlap = detectedOverlap;
                    }
                }
            }

            AppendDelta(frame.Bitmap, activeZone, overlap);
        }

        _previousFrame.Dispose();
        _previousFrame = CloneFrame(frame);
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

        if (_zoneLayout is null)
        {
            throw new InvalidOperationException("At least two frames are required to build a scroll result.");
        }

        var totalWidth = _direction == ScrollDirection.Vertical
            ? _zoneLayout.Value.FixedLeft + _zoneLayout.Value.ScrollBand.Width + _zoneLayout.Value.FixedRight
            : _zoneLayout.Value.FixedLeft + _accumulatedPrimarySize + _zoneLayout.Value.FixedRight;
        var totalHeight = _direction == ScrollDirection.Vertical
            ? _zoneLayout.Value.FixedTop + _accumulatedPrimarySize + _zoneLayout.Value.FixedBottom
            : _zoneLayout.Value.FixedTop + _zoneLayout.Value.ScrollBand.Height + _zoneLayout.Value.FixedBottom;

        return new CaptureResult(
            _segments.Select(segment => new ScrollSegment((Bitmap)segment.Bitmap.Clone(), segment.Offset, segment.TemporaryFilePath)).ToArray(),
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

    private void AppendInitialSegment(Bitmap bitmap, ZoneLayout zoneLayout)
    {
        var bandBitmap = ExtractBandBitmap(bitmap, zoneLayout.ScrollBand);
        var segment = new ScrollSegment(bandBitmap, 0);
        _segments.Add(segment);
        _accumulatedPrimarySize = GetPrimaryAxisSize(bandBitmap);
        _previousBand = PixelBuffer.FromBitmap(bandBitmap);
        SegmentAdded?.Invoke(segment);
        RaisePreviewUpdated();
    }

    private void AppendDelta(Bitmap currentBitmap, ZoneLayout zoneLayout, OverlapResult overlap)
    {
        var bandBitmap = ExtractBandBitmap(currentBitmap, zoneLayout.ScrollBand);
        var currentBand = PixelBuffer.FromBitmap(bandBitmap);

        if (_previousBand is null)
        {
            _previousBand = currentBand;
            return;
        }

        if (overlap.IsIdentical)
        {
            bandBitmap.Dispose();
            _previousBand = currentBand;
            return;
        }

        Bitmap segmentBitmap;
        if (overlap.OverlapPixels <= 0)
        {
            segmentBitmap = bandBitmap;
        }
        else
        {
            var deltaRectangle = _direction == ScrollDirection.Vertical
                ? new Rectangle(0, overlap.OverlapPixels, bandBitmap.Width, bandBitmap.Height - overlap.OverlapPixels)
                : new Rectangle(overlap.OverlapPixels, 0, bandBitmap.Width - overlap.OverlapPixels, bandBitmap.Height);

            if (deltaRectangle.Width <= 0 || deltaRectangle.Height <= 0)
            {
                bandBitmap.Dispose();
                _previousBand = currentBand;
                return;
            }

            segmentBitmap = bandBitmap.Clone(deltaRectangle, PixelFormat.Format32bppArgb);
            bandBitmap.Dispose();
        }

        var segment = new ScrollSegment(segmentBitmap, _accumulatedPrimarySize);
        _segments.Add(segment);
        _accumulatedPrimarySize += GetPrimaryAxisSize(segmentBitmap);
        _previousBand = currentBand;
        SegmentAdded?.Invoke(segment);
        RaisePreviewUpdated();
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
        if (PreviewUpdated is null || _segments.Count == 0 || _zoneLayout is null)
        {
            return;
        }

        var previewWidth = _direction == ScrollDirection.Vertical
            ? _zoneLayout.Value.ScrollBand.Width
            : _segments.Sum(segment => segment.Bitmap.Width);
        var previewHeight = _direction == ScrollDirection.Vertical
            ? _segments.Sum(segment => segment.Bitmap.Height)
            : _zoneLayout.Value.ScrollBand.Height;

        var previewBitmap = new Bitmap(previewWidth, previewHeight, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(previewBitmap))
        {
            graphics.Clear(Color.Transparent);
            var offset = 0;
            foreach (var segment in _segments.OrderBy(segment => segment.Offset))
            {
                graphics.DrawImageUnscaled(
                    segment.Bitmap,
                    _direction == ScrollDirection.Vertical ? 0 : offset,
                    _direction == ScrollDirection.Vertical ? offset : 0);
                offset += GetPrimaryAxisSize(segment.Bitmap);
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

    private Bitmap ExtractBandBitmap(Bitmap bitmap, ScreenRect band)
    {
        return bitmap.Clone(new Rectangle(band.X, band.Y, band.Width, band.Height), PixelFormat.Format32bppArgb);
    }

    private PixelBufferSnapshot CreateBandSnapshot(Bitmap bitmap, ScreenRect band)
    {
        using var bandBitmap = ExtractBandBitmap(bitmap, band);
        return PixelBuffer.FromBitmap(bandBitmap);
    }

    private ZoneLayout MergeConservativeZone(ZoneLayout activeZone, ZoneLayout detectedZone)
    {
        if (_direction == ScrollDirection.Vertical)
        {
            var fixedTop = Math.Min(activeZone.FixedTop, detectedZone.FixedTop);
            var fixedBottom = Math.Min(activeZone.FixedBottom, detectedZone.FixedBottom);
            return new ZoneLayout(
                fixedTop,
                fixedBottom,
                0,
                0,
                new ScreenRect(0, fixedTop, _region.Width, _region.Height - fixedTop - fixedBottom));
        }

        var fixedLeft = Math.Min(activeZone.FixedLeft, detectedZone.FixedLeft);
        var fixedRight = Math.Min(activeZone.FixedRight, detectedZone.FixedRight);
        return new ZoneLayout(
            0,
            0,
            fixedLeft,
            fixedRight,
            new ScreenRect(fixedLeft, 0, _region.Width - fixedLeft - fixedRight, _region.Height));
    }

    private OverlapResult FindOverlap(PixelBufferSnapshot previousBand, Bitmap currentBitmap, ZoneLayout zoneLayout)
    {
        using var currentBandBitmap = ExtractBandBitmap(currentBitmap, zoneLayout.ScrollBand);
        var currentBand = PixelBuffer.FromBitmap(currentBandBitmap);
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

    private void Reset()
    {
        _previousFrame?.Dispose();
        _previousFrame = null;
        _previousBand = null;
        _zoneLayout = null;
        _frameCount = 0;
        _accumulatedPrimarySize = 0;
        _started = false;
        _finished = false;

        foreach (var segment in _segments)
        {
            segment.Dispose();
        }

        _segments.Clear();
        DisposeFixedRegions();
    }
}
