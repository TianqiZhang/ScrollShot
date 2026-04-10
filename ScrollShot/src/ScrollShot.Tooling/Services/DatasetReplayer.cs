using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using ScrollShot.Capture.Models;
using ScrollShot.Editor.Composition;
using ScrollShot.Editor.Models;
using ScrollShot.Scroll;
using ScrollShot.Scroll.Shared;
using ScrollShot.StitchingData.Models;
using ScrollShot.StitchingData.Services;
using ScrollShot.Tooling.Models;

namespace ScrollShot.Tooling.Services;

public sealed class DatasetReplayer
{
    public ReplayReport Replay(ReplayCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var replayStopwatch = Stopwatch.StartNew();
        long frameLoadElapsedMilliseconds = 0;
        long stitchElapsedMilliseconds = 0;
        long composeElapsedMilliseconds = 0;
        var manifest = ManifestStore.Load(options.ManifestPath);
        var manifestDirectory = Path.GetDirectoryName(options.ManifestPath)
                                ?? throw new InvalidOperationException("The manifest path must include a directory.");
        if (options.PersistOutputImage || options.PersistReplayReport)
        {
            Directory.CreateDirectory(options.OutputDirectory);
        }

        using var session = new ScrollSessionFactory(options.ProfileName).CreateSession();
        var region = new ScreenRect(0, 0, manifest.ViewportWidth, manifest.ViewportHeight);
        session.Start(region, manifest.Direction);

        try
        {
            foreach (var frame in manifest.Frames.OrderBy(frame => frame.Index))
            {
                var frameLoadStopwatch = Stopwatch.StartNew();
                var bitmap = LoadArgbBitmap(Path.Combine(manifestDirectory, frame.RelativePath));
                frameLoadStopwatch.Stop();
                frameLoadElapsedMilliseconds += frameLoadStopwatch.ElapsedMilliseconds;
                using var capturedFrame = new CapturedFrame(bitmap, region, DateTimeOffset.UtcNow);

                var stitchStopwatch = Stopwatch.StartNew();
                session.ProcessFrame(capturedFrame);
                stitchStopwatch.Stop();
                stitchElapsedMilliseconds += stitchStopwatch.ElapsedMilliseconds;
            }

            var finishStopwatch = Stopwatch.StartNew();
            session.Finish();
            var result = session.GetResult();
            finishStopwatch.Stop();
            stitchElapsedMilliseconds += finishStopwatch.ElapsedMilliseconds;
            try
            {
                string? outputImageRelativePath = null;
                var composeStopwatch = Stopwatch.StartNew();
                using var composed = new ImageCompositor().Compose(result, EditState.Default);
                if (options.PersistOutputImage)
                {
                    outputImageRelativePath = "stitched.png";
                    var outputImagePath = Path.Combine(options.OutputDirectory, outputImageRelativePath);
                    composed.Save(outputImagePath, ImageFormat.Png);
                }

                composeStopwatch.Stop();
                composeElapsedMilliseconds = composeStopwatch.ElapsedMilliseconds;
                replayStopwatch.Stop();

                var report = new ReplayReport
                {
                    Succeeded = true,
                    DatasetName = manifest.Name,
                    ProfileName = options.ProfileName,
                    FrameCount = manifest.Frames.Count,
                    SegmentCount = result.Segments.Count,
                    OutputWidth = composed.Width,
                    OutputHeight = composed.Height,
                    OutputImageRelativePath = outputImageRelativePath,
                    ReplayElapsedMilliseconds = replayStopwatch.ElapsedMilliseconds,
                    FrameLoadElapsedMilliseconds = frameLoadElapsedMilliseconds,
                    StitchElapsedMilliseconds = stitchElapsedMilliseconds,
                    ComposeElapsedMilliseconds = composeElapsedMilliseconds,
                    Direction = manifest.Direction,
                };

                report = AddGroundTruthComparison(report, manifest, manifestDirectory, composed);
                if (options.PersistReplayReport)
                {
                    ManifestStore.SaveReplayReport(report, Path.Combine(options.OutputDirectory, "report.json"));
                }

                return report;
            }
            finally
            {
                DisposeCaptureResult(result);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            replayStopwatch.Stop();
            var report = new ReplayReport
            {
                Succeeded = false,
                ErrorMessage = exception.Message,
                DatasetName = manifest.Name,
                ProfileName = options.ProfileName,
                FrameCount = manifest.Frames.Count,
                ReplayElapsedMilliseconds = replayStopwatch.ElapsedMilliseconds,
                FrameLoadElapsedMilliseconds = frameLoadElapsedMilliseconds,
                StitchElapsedMilliseconds = stitchElapsedMilliseconds,
                ComposeElapsedMilliseconds = composeElapsedMilliseconds,
                Direction = manifest.Direction,
            };

            if (options.PersistReplayReport)
            {
                ManifestStore.SaveReplayReport(report, Path.Combine(options.OutputDirectory, "report.json"));
            }

            return report;
        }
    }

    private static ReplayReport AddGroundTruthComparison(
        ReplayReport report,
        StitchDatasetManifest manifest,
        string manifestDirectory,
        Bitmap composed)
    {
        if (manifest.Truth is null)
        {
            return report;
        }

        var groundTruthPath = Path.Combine(manifestDirectory, manifest.Truth.GroundTruthRelativePath);
        if (!File.Exists(groundTruthPath))
        {
            return report;
        }

        using var groundTruth = LoadArgbBitmap(groundTruthPath);
        var dimensionsMatch = groundTruth.Width == composed.Width && groundTruth.Height == composed.Height;
        double? normalizedDifference = null;

        if (dimensionsMatch)
        {
            var groundTruthPixels = PixelBuffer.FromBitmap(groundTruth);
            var composedPixels = PixelBuffer.FromBitmap(composed);
            normalizedDifference = PixelBuffer.ComputeNormalizedDifference(groundTruthPixels.Pixels, composedPixels.Pixels);
        }

        return report with
        {
            GroundTruthDimensionsMatch = dimensionsMatch,
            NormalizedDifferenceToGroundTruth = normalizedDifference,
        };
    }

    private static Bitmap LoadArgbBitmap(string path)
    {
        using var bitmap = new Bitmap(path);
        return bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), PixelFormat.Format32bppArgb);
    }

    private static void DisposeCaptureResult(ScrollShot.Scroll.Models.CaptureResult result)
    {
        foreach (var segment in result.Segments)
        {
            segment.Bitmap.Dispose();
        }

        result.FixedTopBitmap?.Dispose();
        result.FixedBottomBitmap?.Dispose();
        result.FixedLeftBitmap?.Dispose();
        result.FixedRightBitmap?.Dispose();
    }
}
