using System.Drawing;
using System.Drawing.Imaging;
using ScrollShot.Capture.Models;
using ScrollShot.Editor.Composition;
using ScrollShot.Editor.Models;
using ScrollShot.Scroll;
using ScrollShot.Scroll.Algorithms;
using ScrollShot.Tooling.Models;

namespace ScrollShot.Tooling.Services;

public sealed class DatasetReplayer
{
    public ReplayReport Replay(ReplayCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var manifest = ManifestStore.Load(options.ManifestPath);
        var manifestDirectory = Path.GetDirectoryName(options.ManifestPath)
                                ?? throw new InvalidOperationException("The manifest path must include a directory.");
        Directory.CreateDirectory(options.OutputDirectory);

        using var session = new ScrollSession();
        var region = new ScreenRect(0, 0, manifest.ViewportWidth, manifest.ViewportHeight);
        session.Start(region, manifest.Direction);

        try
        {
            foreach (var frame in manifest.Frames.OrderBy(frame => frame.Index))
            {
                using var capturedFrame = new CapturedFrame(
                    LoadArgbBitmap(Path.Combine(manifestDirectory, frame.RelativePath)),
                    region,
                    DateTimeOffset.UtcNow);
                session.ProcessFrame(capturedFrame);
            }

            session.Finish();
            var result = session.GetResult();
            try
            {
                var outputImageRelativePath = "stitched.png";
                var outputImagePath = Path.Combine(options.OutputDirectory, outputImageRelativePath);
                using var composed = new ImageCompositor().Compose(result, EditState.Default);
                composed.Save(outputImagePath, ImageFormat.Png);

                var report = new ReplayReport
                {
                    Succeeded = true,
                    FrameCount = manifest.Frames.Count,
                    SegmentCount = result.Segments.Count,
                    OutputWidth = composed.Width,
                    OutputHeight = composed.Height,
                    OutputImageRelativePath = outputImageRelativePath,
                    Direction = manifest.Direction,
                };

                report = AddGroundTruthComparison(report, manifest, manifestDirectory, composed);
                ManifestStore.SaveReplayReport(report, Path.Combine(options.OutputDirectory, "report.json"));
                return report;
            }
            finally
            {
                DisposeCaptureResult(result);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            var report = new ReplayReport
            {
                Succeeded = false,
                ErrorMessage = exception.Message,
                FrameCount = manifest.Frames.Count,
                Direction = manifest.Direction,
            };

            ManifestStore.SaveReplayReport(report, Path.Combine(options.OutputDirectory, "report.json"));
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
