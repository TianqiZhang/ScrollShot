using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ScrollShot.Editor.Models;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace ScrollShot.Editor.Controls;

public enum ImageViewportInteractionMode
{
    Pan,
    Crop,
}

public partial class ImageViewport : UserControl
{
    private Point? _lastPanPoint;
    private Point? _cropStart;
    private BitmapSource? _image;
    private ImageViewportInteractionMode _interactionMode;

    public ImageViewport()
    {
        InitializeComponent();
        UpdateInteractionVisuals();
    }

    public event EventHandler<CropRect?>? CropChanged;

    public event EventHandler<double>? ZoomChanged;

    public double ZoomFactor => ScaleTransform.ScaleX;

    public void SetImage(BitmapSource? image)
    {
        _image = image;
        ViewportImage.Source = image;
        OverlayCanvas.Width = image?.PixelWidth ?? 0;
        OverlayCanvas.Height = image?.PixelHeight ?? 0;
    }

    public void SetCrop(CropRect? cropRect)
    {
        if (cropRect is null)
        {
            CropRectangle.Visibility = Visibility.Collapsed;
            return;
        }

        CropRectangle.Visibility = Visibility.Visible;
        Canvas.SetLeft(CropRectangle, cropRect.Value.X);
        Canvas.SetTop(CropRectangle, cropRect.Value.Y);
        CropRectangle.Width = cropRect.Value.Width;
        CropRectangle.Height = cropRect.Value.Height;
    }

    public void FitToView()
    {
        if (_image is null || ScrollViewer.ViewportWidth <= 0 || ScrollViewer.ViewportHeight <= 0)
        {
            SetZoom(1);
            return;
        }

        var fitScale = Math.Min(
            ScrollViewer.ViewportWidth / _image.PixelWidth,
            ScrollViewer.ViewportHeight / _image.PixelHeight);
        SetZoom(Math.Clamp(fitScale, 0.1, 5.0));
    }

    public void SetOneToOne()
    {
        SetZoom(1);
    }

    public void ZoomIn()
    {
        SetZoom(ScaleTransform.ScaleX + 0.1);
    }

    public void ZoomOut()
    {
        SetZoom(ScaleTransform.ScaleX - 0.1);
    }

    public void SetInteractionMode(ImageViewportInteractionMode interactionMode)
    {
        _interactionMode = interactionMode;
        UpdateInteractionVisuals();
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        SetZoom(ScaleTransform.ScaleX + (e.Delta > 0 ? 0.1 : -0.1));
        e.Handled = true;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_interactionMode == ImageViewportInteractionMode.Crop)
        {
            _cropStart = e.GetPosition(OverlayCanvas);
            CropRectangle.Visibility = Visibility.Visible;
            Canvas.SetLeft(CropRectangle, _cropStart.Value.X);
            Canvas.SetTop(CropRectangle, _cropStart.Value.Y);
            CropRectangle.Width = 0;
            CropRectangle.Height = 0;
        }
        else
        {
            _lastPanPoint = e.GetPosition(this);
        }

        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_cropStart is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            var current = e.GetPosition(OverlayCanvas);
            var rectangle = new Rect(_cropStart.Value, current);
            Canvas.SetLeft(CropRectangle, rectangle.X);
            Canvas.SetTop(CropRectangle, rectangle.Y);
            CropRectangle.Width = rectangle.Width;
            CropRectangle.Height = rectangle.Height;
        }
        else if (_lastPanPoint is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            var current = e.GetPosition(this);
            var delta = current - _lastPanPoint.Value;
            ScrollViewer.ScrollToHorizontalOffset(ScrollViewer.HorizontalOffset - delta.X);
            ScrollViewer.ScrollToVerticalOffset(ScrollViewer.VerticalOffset - delta.Y);
            _lastPanPoint = current;
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_cropStart is not null)
        {
            var current = e.GetPosition(OverlayCanvas);
            var rectangle = new Rect(_cropStart.Value, current);
            CropChanged?.Invoke(
                this,
                rectangle.Width < 2 || rectangle.Height < 2
                    ? null
                    : new CropRect((int)rectangle.X, (int)rectangle.Y, (int)rectangle.Width, (int)rectangle.Height));
        }

        _lastPanPoint = null;
        _cropStart = null;
        ReleaseMouseCapture();
    }

    private void SetZoom(double zoom)
    {
        var normalizedZoom = Math.Clamp(zoom, 0.1, 5.0);
        ScaleTransform.ScaleX = normalizedZoom;
        ScaleTransform.ScaleY = normalizedZoom;
        ZoomChanged?.Invoke(this, normalizedZoom);
    }

    private void UpdateInteractionVisuals()
    {
        Cursor = _interactionMode == ImageViewportInteractionMode.Crop
            ? Cursors.Cross
            : Cursors.SizeAll;
    }
}
