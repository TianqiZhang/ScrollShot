using System.Drawing;
using System.Drawing.Imaging;
using ScrollShot.Editor.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Editor.Composition;

public sealed class ImageCompositor : IImageCompositor
{
    public Bitmap Compose(CaptureResult result, EditState editState)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(editState);

        using var baseBitmap = ComposeBase(result, editState.IncludeChrome);
        using var editedBitmap = ApplyPrimaryAxisEdits(baseBitmap, result.Direction, editState.TrimRange, editState.CutRanges);
        return ApplyCrop(editedBitmap, editState.CropRect);
    }

    private static Bitmap ComposeBase(CaptureResult result, bool includeChrome)
    {
        var segmentPrimarySize = result.Segments.Sum(segment => result.Direction == ScrollDirection.Vertical ? segment.Bitmap.Height : segment.Bitmap.Width);
        var width = result.Direction == ScrollDirection.Vertical
            ? (includeChrome ? (result.FixedLeftBitmap?.Width ?? 0) + result.ZoneLayout.ScrollBand.Width + (result.FixedRightBitmap?.Width ?? 0) : result.ZoneLayout.ScrollBand.Width)
            : (includeChrome ? segmentPrimarySize + (result.FixedLeftBitmap?.Width ?? 0) + (result.FixedRightBitmap?.Width ?? 0) : segmentPrimarySize);
        var height = result.Direction == ScrollDirection.Vertical
            ? (includeChrome ? segmentPrimarySize + (result.FixedTopBitmap?.Height ?? 0) + (result.FixedBottomBitmap?.Height ?? 0) : segmentPrimarySize)
            : (includeChrome ? (result.FixedTopBitmap?.Height ?? 0) + result.ZoneLayout.ScrollBand.Height + (result.FixedBottomBitmap?.Height ?? 0) : result.ZoneLayout.ScrollBand.Height);

        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("The capture result does not contain composable image data.");
        }

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);

        var leftInset = includeChrome ? result.FixedLeftBitmap?.Width ?? 0 : 0;
        var topInset = includeChrome ? result.FixedTopBitmap?.Height ?? 0 : 0;
        var offsetX = leftInset;
        var offsetY = topInset;

        if (includeChrome && result.Direction == ScrollDirection.Vertical && result.FixedTopBitmap is not null)
        {
            DrawBitmapPixelExact(graphics, result.FixedTopBitmap, 0, 0);
        }

        if (includeChrome && result.Direction == ScrollDirection.Horizontal && result.FixedLeftBitmap is not null)
        {
            DrawBitmapPixelExact(graphics, result.FixedLeftBitmap, 0, 0);
        }

        if (includeChrome && result.Direction == ScrollDirection.Horizontal && result.FixedTopBitmap is not null)
        {
            graphics.DrawImage(result.FixedTopBitmap, new Rectangle(offsetX, 0, segmentPrimarySize, result.FixedTopBitmap.Height));
        }

        foreach (var segment in result.Segments.OrderBy(segment => segment.Offset))
        {
            DrawBitmapPixelExact(graphics, segment.Bitmap, offsetX, offsetY);

            if (result.Direction == ScrollDirection.Vertical)
            {
                offsetY += segment.Bitmap.Height;
            }
            else
            {
                offsetX += segment.Bitmap.Width;
            }
        }

        if (includeChrome && result.Direction == ScrollDirection.Vertical && result.FixedBottomBitmap is not null)
        {
            DrawBitmapPixelExact(graphics, result.FixedBottomBitmap, 0, topInset + segmentPrimarySize);
        }

        if (includeChrome && result.Direction == ScrollDirection.Horizontal && result.FixedRightBitmap is not null)
        {
            DrawBitmapPixelExact(graphics, result.FixedRightBitmap, leftInset + segmentPrimarySize, 0);
        }

        if (includeChrome && result.Direction == ScrollDirection.Horizontal && result.FixedBottomBitmap is not null)
        {
            graphics.DrawImage(result.FixedBottomBitmap, new Rectangle(leftInset, topInset + result.ZoneLayout.ScrollBand.Height, segmentPrimarySize, result.FixedBottomBitmap.Height));
        }

        if (includeChrome && result.Direction == ScrollDirection.Vertical && result.FixedLeftBitmap is not null)
        {
            graphics.DrawImage(result.FixedLeftBitmap, new Rectangle(0, topInset, result.FixedLeftBitmap.Width, segmentPrimarySize));
        }

        if (includeChrome && result.Direction == ScrollDirection.Vertical && result.FixedRightBitmap is not null)
        {
            graphics.DrawImage(result.FixedRightBitmap, new Rectangle(leftInset + result.ZoneLayout.ScrollBand.Width, topInset, result.FixedRightBitmap.Width, segmentPrimarySize));
        }

        return bitmap;
    }

    private static void DrawBitmapPixelExact(Graphics graphics, Bitmap bitmap, int x, int y)
    {
        graphics.DrawImage(
            bitmap,
            new Rectangle(x, y, bitmap.Width, bitmap.Height),
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            GraphicsUnit.Pixel);
    }

    private static Bitmap ApplyPrimaryAxisEdits(Bitmap source, ScrollDirection direction, TrimRange trimRange, IReadOnlyList<CutRange> cuts)
    {
        var primarySize = direction == ScrollDirection.Vertical ? source.Height : source.Width;
        var trimmedEnd = Math.Max(trimRange.HeadTrimPixels, primarySize - trimRange.TailTrimPixels);
        var removedRanges = new List<CutRange>();

        if (trimRange.HeadTrimPixels > 0)
        {
            removedRanges.Add(new CutRange(0, trimRange.HeadTrimPixels));
        }

        removedRanges.AddRange(cuts);

        if (trimmedEnd < primarySize)
        {
            removedRanges.Add(new CutRange(trimmedEnd, primarySize));
        }

        if (removedRanges.Count == 0)
        {
            return (Bitmap)source.Clone();
        }

        var mergedRanges = removedRanges
            .OrderBy(range => range.StartPixel)
            .Aggregate(new List<CutRange>(), (list, next) =>
            {
                if (list.Count == 0)
                {
                    list.Add(next);
                    return list;
                }

                var last = list[^1];
                if (next.StartPixel <= last.EndPixel)
                {
                    list[^1] = new CutRange(last.StartPixel, Math.Max(last.EndPixel, next.EndPixel));
                }
                else
                {
                    list.Add(next);
                }

                return list;
            });

        var keptRanges = new List<CutRange>();
        var cursor = 0;

        foreach (var range in mergedRanges)
        {
            if (range.StartPixel > cursor)
            {
                keptRanges.Add(new CutRange(cursor, range.StartPixel));
            }

            cursor = range.EndPixel;
        }

        if (cursor < primarySize)
        {
            keptRanges.Add(new CutRange(cursor, primarySize));
        }

        var outputPrimarySize = keptRanges.Sum(range => range.EndPixel - range.StartPixel);
        var outputWidth = direction == ScrollDirection.Vertical ? source.Width : outputPrimarySize;
        var outputHeight = direction == ScrollDirection.Vertical ? outputPrimarySize : source.Height;
        var output = new Bitmap(outputWidth, outputHeight, PixelFormat.Format32bppArgb);

        using var graphics = Graphics.FromImage(output);
        var destinationOffset = 0;

        foreach (var range in keptRanges)
        {
            var length = range.EndPixel - range.StartPixel;
            var sourceRectangle = direction == ScrollDirection.Vertical
                ? new Rectangle(0, range.StartPixel, source.Width, length)
                : new Rectangle(range.StartPixel, 0, length, source.Height);
            var destinationRectangle = direction == ScrollDirection.Vertical
                ? new Rectangle(0, destinationOffset, source.Width, length)
                : new Rectangle(destinationOffset, 0, length, source.Height);

            graphics.DrawImage(source, destinationRectangle, sourceRectangle, GraphicsUnit.Pixel);
            destinationOffset += length;
        }

        return output;
    }

    private static Bitmap ApplyCrop(Bitmap source, CropRect? cropRect)
    {
        if (cropRect is null)
        {
            return (Bitmap)source.Clone();
        }

        var crop = cropRect.Value;
        var rectangle = new Rectangle(crop.X, crop.Y, crop.Width, crop.Height);
        return source.Clone(rectangle, PixelFormat.Format32bppArgb);
    }
}
