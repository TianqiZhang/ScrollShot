using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using ScrollShot.Capture.Models;
using ScrollShot.Editor.Composition;
using ScrollShot.Editor.Models;
using ScrollShot.Scroll.Models;
using ScrollShot.StitchingData.Models;
using ScrollShot.StitchingData.Services;
using Bitmap = System.Drawing.Bitmap;

namespace ScrollShot.App.Services;

internal sealed class ScrollCaptureDebugDumpSession : IDisposable
{
    private readonly string _datasetName;
    private readonly string _datasetDirectory;
    private readonly string _framesDirectory;
    private readonly ScreenRect _region;
    private readonly ScrollDirection _direction;
    private readonly int _samplingIntervalMilliseconds;
    private readonly DateTimeOffset _startedAtUtc;
    private readonly List<StitchDatasetFrame> _frames = new();
    private bool _finalized;

    private ScrollCaptureDebugDumpSession(
        string datasetName,
        string datasetDirectory,
        ScreenRect region,
        ScrollDirection direction,
        int samplingIntervalMilliseconds,
        DateTimeOffset startedAtUtc)
    {
        _datasetName = datasetName;
        _datasetDirectory = datasetDirectory;
        _framesDirectory = Path.Combine(datasetDirectory, "frames");
        _region = region;
        _direction = direction;
        _samplingIntervalMilliseconds = samplingIntervalMilliseconds;
        _startedAtUtc = startedAtUtc;
    }

    public string OutputDirectory => _datasetDirectory;

    public string? FailureMessage { get; private set; }

    public static ScrollCaptureDebugDumpSession Create(
        string rootDirectory,
        ScreenRect region,
        ScrollDirection direction,
        TimeSpan samplingInterval)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("The debug dump folder must not be empty.", nameof(rootDirectory));
        }

        var startedAtUtc = DateTimeOffset.UtcNow;
        var datasetName = $"debug-{startedAtUtc:yyyyMMdd-HHmmssfff}";
        var datasetDirectory = Path.Combine(rootDirectory, datasetName);
        Directory.CreateDirectory(datasetDirectory);
        Directory.CreateDirectory(Path.Combine(datasetDirectory, "frames"));

        return new ScrollCaptureDebugDumpSession(
            datasetName,
            datasetDirectory,
            region,
            direction,
            (int)Math.Round(samplingInterval.TotalMilliseconds),
            startedAtUtc);
    }

    public void RecordFrame(CapturedFrame frame, string? trace = null)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (_finalized || FailureMessage is not null)
        {
            return;
        }

        var index = _frames.Count;
        var fileName = $"frame_{index:D4}.png";
        var relativePath = Path.Combine("frames", fileName);
        var fullPath = Path.Combine(_framesDirectory, fileName);

        if (!TryRun(
                () =>
                {
                    using var bitmapClone = (Bitmap)frame.Bitmap.Clone();
                    bitmapClone.Save(fullPath, ImageFormat.Png);
                },
                "Saving a captured debug frame"))
        {
            return;
        }

        _frames.Add(new StitchDatasetFrame
        {
            Index = index,
            RelativePath = relativePath,
            CapturedAtUtc = frame.CapturedAtUtc,
            ElapsedMilliseconds = (long)Math.Round((frame.CapturedAtUtc - _startedAtUtc).TotalMilliseconds),
            Width = frame.Bitmap.Width,
            Height = frame.Bitmap.Height,
            Trace = trace,
        });
    }

    public void Complete(CaptureResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (_finalized)
        {
            return;
        }

        var report = default(ReplayReport);
        var savedOutput = TryRun(
                () =>
                {
                    const string outputImageRelativePath = "stitched.png";
                    using var composed = new ImageCompositor().Compose(result, EditState.Default);
                    composed.Save(Path.Combine(_datasetDirectory, outputImageRelativePath), ImageFormat.Png);
                    report = new ReplayReport
                    {
                        Succeeded = true,
                        FrameCount = _frames.Count,
                        SegmentCount = result.Segments.Count,
                        OutputWidth = composed.Width,
                        OutputHeight = composed.Height,
                        OutputImageRelativePath = outputImageRelativePath,
                        Direction = result.Direction,
                    };
                },
                "Saving the stitched debug output");
        if (savedOutput && report is not null)
        {
            TryRun(
                () => ManifestStore.SaveReplayReport(report, Path.Combine(_datasetDirectory, "report.json")),
                "Saving the debug replay report");
        }

        FinalizeManifest("completed");
    }

    public void Cancel(string reason)
    {
        if (_finalized)
        {
            return;
        }

        FinalizeManifest(reason);
    }

    public void Dispose()
    {
        Cancel("closed");
    }

    private void FinalizeManifest(string completionReason)
    {
        if (_finalized)
        {
            return;
        }

        _finalized = true;
        TryRun(
            () => ManifestStore.Save(BuildManifest(completionReason), Path.Combine(_datasetDirectory, "manifest.json")),
            "Saving the debug dataset manifest");
    }

    private StitchDatasetManifest BuildManifest(string completionReason)
    {
        return new StitchDatasetManifest
        {
            Name = _datasetName,
            Source = "live-capture",
            Direction = _direction,
            ViewportWidth = _region.Width,
            ViewportHeight = _region.Height,
            StepPixels = 0,
            CaptureRegion = new StitchCaptureRegion
            {
                X = _region.X,
                Y = _region.Y,
                Width = _region.Width,
                Height = _region.Height,
            },
            SamplingIntervalMilliseconds = _samplingIntervalMilliseconds,
            CreatedAtUtc = _startedAtUtc,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            CompletionReason = completionReason,
            FailureMessage = FailureMessage,
            Frames = _frames.ToArray(),
        };
    }

    private bool TryRun(Action operation, string description)
    {
        try
        {
            operation();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ExternalException or ArgumentException)
        {
            FailureMessage ??= $"{description} failed in '{_datasetDirectory}': {exception.Message}";
            return false;
        }
    }
}
