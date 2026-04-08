using System.Drawing;
using System.Drawing.Imaging;
using ScrollShot.StitchingData.Models;
using ScrollShot.StitchingData.Services;
using ScrollShot.Tooling.Models;

namespace ScrollShot.Tooling.Services;

public sealed class LongScreenshotSlicer
{
    public StitchDatasetManifest Slice(SliceCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!File.Exists(options.InputImagePath))
        {
            throw new FileNotFoundException("The input image was not found.", options.InputImagePath);
        }

        using var source = new Bitmap(options.InputImagePath);
        return Slice(source, options);
    }

    public StitchDatasetManifest Slice(Bitmap source, SliceCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        var viewportWidth = options.ViewportWidth ?? (source.Width - options.CropX);
        if (viewportWidth <= 0 || viewportWidth > source.Width)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ViewportWidth), "Viewport width must fit within the source image.");
        }

        if (options.CropX < 0 || options.CropX + viewportWidth > source.Width)
        {
            throw new ArgumentOutOfRangeException(nameof(options.CropX), "Crop X must keep the viewport within the source image.");
        }

        if (options.ViewportHeight <= 0 || options.ViewportHeight > source.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ViewportHeight), "Viewport height must fit within the source image.");
        }

        var stepPixels = ResolveStepPixels(options.ViewportHeight, options.StepPixels, options.OverlapPixels);
        var datasetName = string.IsNullOrWhiteSpace(options.DatasetName)
            ? Path.GetFileNameWithoutExtension(options.InputImagePath)
            : options.DatasetName.Trim();

        Directory.CreateDirectory(options.OutputDirectory);
        var framesDirectory = Path.Combine(options.OutputDirectory, "frames");
        Directory.CreateDirectory(framesDirectory);

        using var groundTruth = source.Clone(
            new Rectangle(options.CropX, 0, viewportWidth, source.Height),
            PixelFormat.Format32bppArgb);
        var groundTruthRelativePath = "groundtruth.png";
        var groundTruthPath = Path.Combine(options.OutputDirectory, groundTruthRelativePath);
        groundTruth.Save(groundTruthPath, ImageFormat.Png);

        var offsets = BuildOffsets(groundTruth.Height, options.ViewportHeight, stepPixels);
        var frames = new List<StitchDatasetFrame>(offsets.Count);

        for (var index = 0; index < offsets.Count; index++)
        {
            var offset = offsets[index];
            using var frameBitmap = groundTruth.Clone(
                new Rectangle(0, offset, viewportWidth, options.ViewportHeight),
                PixelFormat.Format32bppArgb);
            var frameFileName = $"frame_{index:D4}.png";
            var frameRelativePath = Path.Combine("frames", frameFileName);
            var framePath = Path.Combine(options.OutputDirectory, frameRelativePath);
            frameBitmap.Save(framePath, ImageFormat.Png);

            frames.Add(new StitchDatasetFrame
            {
                Index = index,
                RelativePath = frameRelativePath,
                OffsetPixels = offset,
                ExpectedOverlapWithPreviousPixels = index == 0 ? null : options.ViewportHeight - (offset - offsets[index - 1]),
                Width = frameBitmap.Width,
                Height = frameBitmap.Height,
            });
        }

        var manifest = new StitchDatasetManifest
        {
            Name = datasetName,
            Source = "synthetic",
            ViewportWidth = viewportWidth,
            ViewportHeight = options.ViewportHeight,
            StepPixels = stepPixels,
            CaptureRegion = new StitchCaptureRegion
            {
                X = options.CropX,
                Y = 0,
                Width = viewportWidth,
                Height = options.ViewportHeight,
            },
            CompletionReason = "generated",
            Truth = new StitchDatasetTruth
            {
                GroundTruthRelativePath = groundTruthRelativePath,
                GroundTruthWidth = groundTruth.Width,
                GroundTruthHeight = groundTruth.Height,
            },
            Frames = frames,
        };

        ManifestStore.Save(manifest, Path.Combine(options.OutputDirectory, "manifest.json"));
        return manifest;
    }

    internal static int ResolveStepPixels(int viewportHeight, int? stepPixels, int? overlapPixels)
    {
        if (stepPixels.HasValue && overlapPixels.HasValue)
        {
            throw new ArgumentException("Specify either step pixels or overlap pixels, not both.");
        }

        var resolvedStep = stepPixels ?? (overlapPixels.HasValue ? viewportHeight - overlapPixels.Value : viewportHeight / 2);
        if (resolvedStep <= 0 || resolvedStep > viewportHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(stepPixels), "Step pixels must be between 1 and the viewport height.");
        }

        return resolvedStep;
    }

    internal static List<int> BuildOffsets(int totalHeight, int viewportHeight, int stepPixels)
    {
        var offsets = new List<int> { 0 };
        if (totalHeight == viewportHeight)
        {
            return offsets;
        }

        while (true)
        {
            var current = offsets[^1];
            if (current + viewportHeight >= totalHeight)
            {
                break;
            }

            var next = Math.Min(current + stepPixels, totalHeight - viewportHeight);
            if (next == current)
            {
                break;
            }

            offsets.Add(next);
        }

        return offsets;
    }
}
