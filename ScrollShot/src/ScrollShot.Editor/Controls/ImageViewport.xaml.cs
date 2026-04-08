using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ScrollShot.Editor.Models;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace ScrollShot.Editor.Controls;

public partial class ImageViewport : UserControl
{
    private Point? _lastPanPoint;
    private Point? _cropStart;

    public ImageViewport()
    {
        InitializeComponent();
    }

    public event EventHandler<CropRect?>? CropChanged;

    public void SetImage(BitmapSource? image)
    {
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
        ScaleTransform.ScaleX = 1;
        ScaleTransform.ScaleY = 1;
    }

    public void SetOneToOne()
    {
        ScaleTransform.ScaleX = 1;
        ScaleTransform.ScaleY = 1;
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var delta = e.Delta > 0 ? 0.1 : -0.1;
        var nextScale = Math.Clamp(ScaleTransform.ScaleX + delta, 0.2, 5.0);
        ScaleTransform.ScaleX = nextScale;
        ScaleTransform.ScaleY = nextScale;
        e.Handled = true;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            _cropStart = e.GetPosition(OverlayCanvas);
            CropRectangle.Visibility = Visibility.Visible;
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
}
