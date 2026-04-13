using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ScrollShot.Editor.Models;
using ScrollShot.Scroll.Models;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace ScrollShot.Editor.Controls;

public partial class ImageViewport : UserControl
{
    private const double HandleHitRadius = 6;
    private const double HandleSize = 10;
    private const int MinCropPixels = 2;

    private BitmapSource? _image;
    private CropRect? _cropRect;
    private bool _isCutBandMode;

    // Drag state
    private DragAction _currentDrag = DragAction.None;
    private Point _dragStartImage;
    private CropRect? _dragStartCrop;

    // Right-click pan
    private Point? _rightPanStart;

    // Cut band
    private Point? _cutBandStart;
    private ScrollDirection _cutDirection;

    // Cut band overlays (persistent)
    private readonly List<Rectangle> _cutBandRectangles = new();

    public ImageViewport()
    {
        InitializeComponent();
    }

    public event EventHandler<CropRect?>? CropChanged;

    public event EventHandler<CutRange>? CutRequested;

    public event EventHandler<double>? ZoomChanged;

    public double ZoomFactor => ScaleTransform.ScaleX;

    public bool IsCutBandMode => _isCutBandMode;

    public void SetImage(BitmapSource? image)
    {
        _image = image;
        ViewportImage.Source = image;
        OverlayCanvas.Width = image?.PixelWidth ?? 0;
        OverlayCanvas.Height = image?.PixelHeight ?? 0;
    }

    public void SetCrop(CropRect? cropRect)
    {
        _cropRect = ClampCropToImage(cropRect);
        UpdateCropVisuals();
    }

    public void SetCutBands(IReadOnlyList<CutRange> cutRanges, ScrollDirection direction)
    {
        foreach (var rect in _cutBandRectangles)
        {
            OverlayCanvas.Children.Remove(rect);
        }

        _cutBandRectangles.Clear();
        _cutDirection = direction;

        if (_image is null)
        {
            return;
        }

        foreach (var cut in cutRanges)
        {
            if (!TryGetClampedCutRange(cut, direction, out var startPixel, out var endPixel))
            {
                continue;
            }

            var rect = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(120, 220, 53, 69)),
                IsHitTestVisible = false,
            };

            if (direction == ScrollDirection.Vertical)
            {
                Canvas.SetLeft(rect, 0);
                Canvas.SetTop(rect, startPixel);
                rect.Width = _image.PixelWidth;
                rect.Height = endPixel - startPixel;
            }
            else
            {
                Canvas.SetLeft(rect, startPixel);
                Canvas.SetTop(rect, 0);
                rect.Width = endPixel - startPixel;
                rect.Height = _image.PixelHeight;
            }

            _cutBandRectangles.Add(rect);
            OverlayCanvas.Children.Add(rect);
        }
    }

    public void SetCutBandMode(bool active)
    {
        _isCutBandMode = active;
        Cursor = active ? Cursors.Cross : Cursors.Arrow;
        if (!active)
        {
            _cutBandStart = null;
            CutBandPreview.Visibility = Visibility.Collapsed;
        }
    }

    public bool FitToView()
    {
        if (_image is null ||
            _image.PixelWidth <= 0 ||
            _image.PixelHeight <= 0 ||
            ScrollViewer.ViewportWidth <= 0 ||
            ScrollViewer.ViewportHeight <= 0)
        {
            SetZoom(1, null);
            return false;
        }

        var fitScale = Math.Min(
            ScrollViewer.ViewportWidth / _image.PixelWidth,
            ScrollViewer.ViewportHeight / _image.PixelHeight);
        SetZoom(Math.Clamp(fitScale, 0.1, 5.0), null);
        return true;
    }

    public void SetOneToOne() => SetZoom(1, null);

    public void ZoomIn() => SetZoom(ScaleTransform.ScaleX + 0.1, null);

    public void ZoomOut() => SetZoom(ScaleTransform.ScaleX - 0.1, null);

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mouseInViewport = e.GetPosition(ScrollViewer);
        var newZoom = ScaleTransform.ScaleX + (e.Delta > 0 ? 0.1 : -0.1);
        SetZoom(newZoom, mouseInViewport);
        e.Handled = true;
    }

    private void SetZoom(double zoom, Point? anchorInViewport)
    {
        var oldZoom = ScaleTransform.ScaleX;
        var normalizedZoom = Math.Clamp(zoom, 0.1, 5.0);

        if (anchorInViewport is { } anchor)
        {
            var newHOffset = (anchor.X + ScrollViewer.HorizontalOffset) * (normalizedZoom / oldZoom) - anchor.X;
            var newVOffset = (anchor.Y + ScrollViewer.VerticalOffset) * (normalizedZoom / oldZoom) - anchor.Y;

            ScaleTransform.ScaleX = normalizedZoom;
            ScaleTransform.ScaleY = normalizedZoom;

            ScrollViewer.ScrollToHorizontalOffset(Math.Max(0, newHOffset));
            ScrollViewer.ScrollToVerticalOffset(Math.Max(0, newVOffset));
        }
        else
        {
            ScaleTransform.ScaleX = normalizedZoom;
            ScaleTransform.ScaleY = normalizedZoom;
        }

        ZoomChanged?.Invoke(this, normalizedZoom);
    }

    // --- Left-click interactions ---

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartImage = e.GetPosition(OverlayCanvas);

        if (_isCutBandMode)
        {
            _cutBandStart = _dragStartImage;
            CutBandPreview.Visibility = Visibility.Visible;
            UpdateCutBandPreview(_dragStartImage, _dragStartImage);
            ScrollViewer.CaptureMouse();
            return;
        }

        if (_cropRect is { } crop)
        {
            var handle = HitTestHandle(_dragStartImage, crop);
            if (handle != DragAction.None)
            {
                _currentDrag = handle;
                _dragStartCrop = crop;
                ScrollViewer.CaptureMouse();
                return;
            }

            if (IsInsideCrop(_dragStartImage, crop))
            {
                _currentDrag = DragAction.MoveCrop;
                _dragStartCrop = crop;
                ScrollViewer.CaptureMouse();
                return;
            }
        }

        // Outside crop (or no crop): start new crop
        _currentDrag = DragAction.NewCrop;
        ScrollViewer.CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isCutBandMode && _cutBandStart is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateCutBandPreview(_cutBandStart.Value, ClampToImageBounds(e.GetPosition(OverlayCanvas)));
            return;
        }

        if (_currentDrag == DragAction.None && _rightPanStart is null)
        {
            UpdateCursor(e.GetPosition(OverlayCanvas));
            return;
        }

        if (_rightPanStart is not null)
        {
            var current = e.GetPosition(this);
            var delta = current - _rightPanStart.Value;
            ScrollViewer.ScrollToHorizontalOffset(ScrollViewer.HorizontalOffset - delta.X);
            ScrollViewer.ScrollToVerticalOffset(ScrollViewer.VerticalOffset - delta.Y);
            _rightPanStart = current;
            return;
        }

        var clampedImagePos = ClampToImageBounds(e.GetPosition(OverlayCanvas));

        switch (_currentDrag)
        {
            case DragAction.NewCrop:
                var newRect = MakeRect(_dragStartImage, clampedImagePos);
                if (newRect.Width >= MinCropPixels && newRect.Height >= MinCropPixels)
                {
                    _cropRect = new CropRect((int)newRect.X, (int)newRect.Y, (int)newRect.Width, (int)newRect.Height);
                    UpdateCropVisuals();
                }

                break;

            case DragAction.MoveCrop when _dragStartCrop is { } startCrop:
                var dx = (int)(clampedImagePos.X - _dragStartImage.X);
                var dy = (int)(clampedImagePos.Y - _dragStartImage.Y);
                _cropRect = ClampCropToImage(new CropRect(startCrop.X + dx, startCrop.Y + dy, startCrop.Width, startCrop.Height));
                UpdateCropVisuals();
                break;

            default:
                if (_currentDrag >= DragAction.ResizeTL && _dragStartCrop is { } sc)
                {
                    _cropRect = ResizeCrop(sc, _currentDrag, clampedImagePos, _image?.PixelWidth ?? 0, _image?.PixelHeight ?? 0);
                    UpdateCropVisuals();
                }

                break;
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isCutBandMode && _cutBandStart is not null)
        {
            var endPos = ClampToImageBounds(e.GetPosition(OverlayCanvas));
            CutBandPreview.Visibility = Visibility.Collapsed;
            EmitCutBand(_cutBandStart.Value, endPos);
            _cutBandStart = null;
            ScrollViewer.ReleaseMouseCapture();
            return;
        }

        if (_currentDrag != DragAction.None)
        {
            if (_currentDrag == DragAction.NewCrop)
            {
                var finalPos = ClampToImageBounds(e.GetPosition(OverlayCanvas));
                var finalRect = MakeRect(_dragStartImage, finalPos);
                if (finalRect.Width < MinCropPixels || finalRect.Height < MinCropPixels)
                {
                    _cropRect = null;
                    UpdateCropVisuals();
                    CropChanged?.Invoke(this, null);
                }
                else
                {
                    _cropRect = new CropRect((int)finalRect.X, (int)finalRect.Y, (int)finalRect.Width, (int)finalRect.Height);
                    UpdateCropVisuals();
                    CropChanged?.Invoke(this, _cropRect);
                }
            }
            else
            {
                CropChanged?.Invoke(this, _cropRect);
            }

            _currentDrag = DragAction.None;
            _dragStartCrop = null;
            ScrollViewer.ReleaseMouseCapture();
        }
    }

    // --- Right-click pan ---

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _rightPanStart = e.GetPosition(this);
        ScrollViewer.CaptureMouse();
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _rightPanStart = null;
        ScrollViewer.ReleaseMouseCapture();
    }

    // --- Crop visuals ---

    private void UpdateCropVisuals()
    {
        var handles = new[] { HandleTL, HandleTC, HandleTR, HandleML, HandleMR, HandleBL, HandleBC, HandleBR };

        if (_cropRect is null)
        {
            CropBorder.Visibility = Visibility.Collapsed;
            DimTop.Visibility = Visibility.Collapsed;
            DimBottom.Visibility = Visibility.Collapsed;
            DimLeft.Visibility = Visibility.Collapsed;
            DimRight.Visibility = Visibility.Collapsed;
            foreach (var h in handles)
            {
                h.Visibility = Visibility.Collapsed;
            }

            return;
        }

        var c = _cropRect.Value;
        var imgW = _image?.PixelWidth ?? 0;
        var imgH = _image?.PixelHeight ?? 0;

        // Crop border
        CropBorder.Visibility = Visibility.Visible;
        Canvas.SetLeft(CropBorder, c.X);
        Canvas.SetTop(CropBorder, c.Y);
        CropBorder.Width = c.Width;
        CropBorder.Height = c.Height;

        // Dim overlay (4 rectangles)
        SetDimRect(DimTop, 0, 0, imgW, c.Y);
        SetDimRect(DimBottom, 0, c.Y + c.Height, imgW, imgH - c.Y - c.Height);
        SetDimRect(DimLeft, 0, c.Y, c.X, c.Height);
        SetDimRect(DimRight, c.X + c.Width, c.Y, imgW - c.X - c.Width, c.Height);

        // Handles
        var hs = HandleSize / 2;
        SetHandle(HandleTL, c.X, c.Y, hs);
        SetHandle(HandleTC, c.X + c.Width / 2.0, c.Y, hs);
        SetHandle(HandleTR, c.X + c.Width, c.Y, hs);
        SetHandle(HandleML, c.X, c.Y + c.Height / 2.0, hs);
        SetHandle(HandleMR, c.X + c.Width, c.Y + c.Height / 2.0, hs);
        SetHandle(HandleBL, c.X, c.Y + c.Height, hs);
        SetHandle(HandleBC, c.X + c.Width / 2.0, c.Y + c.Height, hs);
        SetHandle(HandleBR, c.X + c.Width, c.Y + c.Height, hs);
    }

    private static void SetDimRect(Rectangle rect, double x, double y, double w, double h)
    {
        if (w <= 0 || h <= 0)
        {
            rect.Visibility = Visibility.Collapsed;
            return;
        }

        rect.Visibility = Visibility.Visible;
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        rect.Width = w;
        rect.Height = h;
    }

    private static void SetHandle(Rectangle handle, double cx, double cy, double halfSize)
    {
        handle.Visibility = Visibility.Visible;
        Canvas.SetLeft(handle, cx - halfSize);
        Canvas.SetTop(handle, cy - halfSize);
    }

    // --- Hit testing ---

    private static DragAction HitTestHandle(Point imagePos, CropRect crop)
    {
        var points = new (double X, double Y, DragAction Action)[]
        {
            (crop.X, crop.Y, DragAction.ResizeTL),
            (crop.X + crop.Width / 2.0, crop.Y, DragAction.ResizeTC),
            (crop.X + crop.Width, crop.Y, DragAction.ResizeTR),
            (crop.X, crop.Y + crop.Height / 2.0, DragAction.ResizeML),
            (crop.X + crop.Width, crop.Y + crop.Height / 2.0, DragAction.ResizeMR),
            (crop.X, crop.Y + crop.Height, DragAction.ResizeBL),
            (crop.X + crop.Width / 2.0, crop.Y + crop.Height, DragAction.ResizeBC),
            (crop.X + crop.Width, crop.Y + crop.Height, DragAction.ResizeBR),
        };

        foreach (var (x, y, action) in points)
        {
            if (Math.Abs(imagePos.X - x) <= HandleHitRadius &&
                Math.Abs(imagePos.Y - y) <= HandleHitRadius)
            {
                return action;
            }
        }

        return DragAction.None;
    }

    private static bool IsInsideCrop(Point imagePos, CropRect crop)
    {
        return imagePos.X >= crop.X && imagePos.X <= crop.X + crop.Width &&
               imagePos.Y >= crop.Y && imagePos.Y <= crop.Y + crop.Height;
    }

    private static CropRect ResizeCrop(CropRect start, DragAction handle, Point imagePos, int imageWidth, int imageHeight)
    {
        var left = start.X;
        var top = start.Y;
        var right = start.X + start.Width;
        var bottom = start.Y + start.Height;
        var clampedX = Math.Clamp((int)imagePos.X, 0, imageWidth);
        var clampedY = Math.Clamp((int)imagePos.Y, 0, imageHeight);

        switch (handle)
        {
            case DragAction.ResizeTL:
                left = Math.Clamp(clampedX, 0, Math.Max(0, right - MinCropPixels));
                top = Math.Clamp(clampedY, 0, Math.Max(0, bottom - MinCropPixels));
                break;
            case DragAction.ResizeTC:
                top = Math.Clamp(clampedY, 0, Math.Max(0, bottom - MinCropPixels));
                break;
            case DragAction.ResizeTR:
                right = Math.Clamp(clampedX, Math.Min(imageWidth, left + MinCropPixels), imageWidth);
                top = Math.Clamp(clampedY, 0, Math.Max(0, bottom - MinCropPixels));
                break;
            case DragAction.ResizeML:
                left = Math.Clamp(clampedX, 0, Math.Max(0, right - MinCropPixels));
                break;
            case DragAction.ResizeMR:
                right = Math.Clamp(clampedX, Math.Min(imageWidth, left + MinCropPixels), imageWidth);
                break;
            case DragAction.ResizeBL:
                left = Math.Clamp(clampedX, 0, Math.Max(0, right - MinCropPixels));
                bottom = Math.Clamp(clampedY, Math.Min(imageHeight, top + MinCropPixels), imageHeight);
                break;
            case DragAction.ResizeBC:
                bottom = Math.Clamp(clampedY, Math.Min(imageHeight, top + MinCropPixels), imageHeight);
                break;
            case DragAction.ResizeBR:
                right = Math.Clamp(clampedX, Math.Min(imageWidth, left + MinCropPixels), imageWidth);
                bottom = Math.Clamp(clampedY, Math.Min(imageHeight, top + MinCropPixels), imageHeight);
                break;
        }

        return new CropRect(left, top, Math.Max(MinCropPixels, right - left), Math.Max(MinCropPixels, bottom - top));
    }

    private void UpdateCursor(Point imagePos)
    {
        if (_isCutBandMode)
        {
            Cursor = Cursors.Cross;
            return;
        }

        if (_cropRect is { } crop)
        {
            var handle = HitTestHandle(imagePos, crop);
            Cursor = handle switch
            {
                DragAction.ResizeTL or DragAction.ResizeBR => Cursors.SizeNWSE,
                DragAction.ResizeTR or DragAction.ResizeBL => Cursors.SizeNESW,
                DragAction.ResizeTC or DragAction.ResizeBC => Cursors.SizeNS,
                DragAction.ResizeML or DragAction.ResizeMR => Cursors.SizeWE,
                _ => IsInsideCrop(imagePos, crop) ? Cursors.SizeAll : Cursors.Cross,
            };
            return;
        }

        Cursor = Cursors.Cross;
    }

    // --- Cut band ---

    private void UpdateCutBandPreview(Point start, Point current)
    {
        if (_image is null)
        {
            return;
        }

        start = ClampToImageBounds(start);
        current = ClampToImageBounds(current);

        if (_cutDirection == ScrollDirection.Vertical)
        {
            var y1 = Math.Min(start.Y, current.Y);
            var y2 = Math.Max(start.Y, current.Y);
            Canvas.SetLeft(CutBandPreview, 0);
            Canvas.SetTop(CutBandPreview, y1);
            CutBandPreview.Width = _image.PixelWidth;
            CutBandPreview.Height = Math.Max(1, y2 - y1);
        }
        else
        {
            var x1 = Math.Min(start.X, current.X);
            var x2 = Math.Max(start.X, current.X);
            Canvas.SetLeft(CutBandPreview, x1);
            Canvas.SetTop(CutBandPreview, 0);
            CutBandPreview.Width = Math.Max(1, x2 - x1);
            CutBandPreview.Height = _image.PixelHeight;
        }
    }

    private void EmitCutBand(Point start, Point end)
    {
        if (_image is null)
        {
            return;
        }

        start = ClampToImageBounds(start);
        end = ClampToImageBounds(end);

        int startPixel, endPixel;
        if (_cutDirection == ScrollDirection.Vertical)
        {
            startPixel = (int)Math.Min(start.Y, end.Y);
            endPixel = (int)Math.Max(start.Y, end.Y);
        }
        else
        {
            startPixel = (int)Math.Min(start.X, end.X);
            endPixel = (int)Math.Max(start.X, end.X);
        }

        if (endPixel - startPixel > 1)
        {
            var axisLength = _cutDirection == ScrollDirection.Vertical ? _image.PixelHeight : _image.PixelWidth;
            CutRequested?.Invoke(this, new CutRange(Math.Clamp(startPixel, 0, axisLength), Math.Clamp(endPixel, 0, axisLength)));
        }
    }

    private Point ClampToImageBounds(Point point)
    {
        if (_image is null)
        {
            return point;
        }

        return new Point(
            Math.Clamp(point.X, 0, _image.PixelWidth),
            Math.Clamp(point.Y, 0, _image.PixelHeight));
    }

    private CropRect? ClampCropToImage(CropRect? cropRect)
    {
        if (cropRect is null || _image is null)
        {
            return cropRect;
        }

        var width = Math.Min(cropRect.Value.Width, _image.PixelWidth);
        var height = Math.Min(cropRect.Value.Height, _image.PixelHeight);
        var x = Math.Clamp(cropRect.Value.X, 0, Math.Max(0, _image.PixelWidth - width));
        var y = Math.Clamp(cropRect.Value.Y, 0, Math.Max(0, _image.PixelHeight - height));
        return new CropRect(x, y, width, height);
    }

    private bool TryGetClampedCutRange(CutRange cutRange, ScrollDirection direction, out int startPixel, out int endPixel)
    {
        var axisLength = direction == ScrollDirection.Vertical ? _image?.PixelHeight ?? 0 : _image?.PixelWidth ?? 0;
        startPixel = Math.Clamp(cutRange.StartPixel, 0, axisLength);
        endPixel = Math.Clamp(cutRange.EndPixel, 0, axisLength);
        return endPixel > startPixel;
    }

    private static Rect MakeRect(Point a, Point b)
    {
        return new Rect(
            Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
            Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
    }

    private enum DragAction
    {
        None,
        NewCrop,
        MoveCrop,
        ResizeTL,
        ResizeTC,
        ResizeTR,
        ResizeML,
        ResizeMR,
        ResizeBL,
        ResizeBC,
        ResizeBR,
    }
}
