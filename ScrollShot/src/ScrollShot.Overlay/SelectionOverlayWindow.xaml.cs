using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ScrollShot.Capture.Models;
using ScrollShot.Overlay.Controls;
using ScrollShot.Overlay.Helpers;
using ScrollShot.Overlay.Interop;
using ScrollShot.Scroll.Models;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ScrollShot.Overlay;

public partial class SelectionOverlayWindow : Window
{
    private Point? _selectionStart;
    private Rect _selectionRect;
    private ScrollDirection? _captureDirection;

    public SelectionOverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
        PreviewStrip.DoneClicked += OnPreviewStripDoneClicked;
    }

    public event EventHandler<OverlayCaptureRequestedEventArgs>? InstantCaptureRequested;

    public event EventHandler<OverlayCaptureRequestedEventArgs>? ScrollCaptureStarted;

    public event EventHandler? Cancelled;

    public event EventHandler? CaptureCompleted;

    public ScreenRect? SelectedRegion { get; private set; }

    public void UpdatePreview(Bitmap previewBitmap)
    {
        if (_captureDirection is null)
        {
            return;
        }

        Dispatcher.Invoke(() => PreviewStrip.SetPreview(previewBitmap, _captureDirection.Value));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var virtualBounds = ScreenHelper.GetVirtualScreenBounds();
        Left = virtualBounds.Left;
        Top = virtualBounds.Top;
        Width = virtualBounds.Width;
        Height = virtualBounds.Height;
        UpdateShadeRegions();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is not HwndSource source)
        {
            return;
        }

        var windowHandle = source.Handle;
        var style = WindowStyles.GetWindowLongPtr(windowHandle, WindowStyles.GwlExStyle);
        WindowStyles.SetWindowLongPtr(windowHandle, WindowStyles.GwlExStyle, style | WindowStyles.WsExToolWindow);
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _selectionStart = e.GetPosition(RootCanvas);
        _selectionRect = Rect.Empty;
        _captureDirection = null;
        PreviewStrip.Visibility = Visibility.Collapsed;
        CaptureMouse();
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_selectionStart is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPosition = e.GetPosition(RootCanvas);
        _selectionRect = new Rect(_selectionStart.Value, currentPosition);
        UpdateSelectionVisuals();
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_selectionStart is null)
        {
            return;
        }

        ReleaseMouseCapture();
        _selectionStart = null;

        if (_selectionRect.Width < 2 || _selectionRect.Height < 2)
        {
            _selectionRect = Rect.Empty;
            SelectedRegion = null;
            UpdateSelectionVisuals();
            return;
        }

        SelectedRegion = ScreenHelper.ToPhysicalScreenRect(_selectionRect, this);
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (SelectedRegion is null)
        {
            return;
        }

        if (_captureDirection is null)
        {
            _captureDirection = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                ? ScrollDirection.Horizontal
                : ScrollDirection.Vertical;

            PreviewStrip.Visibility = Visibility.Visible;
            PositionPreviewStrip(_captureDirection.Value);
            ScrollCaptureStarted?.Invoke(this, new OverlayCaptureRequestedEventArgs(SelectedRegion.Value, _captureDirection));
        }

        e.Handled = true;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Cancelled?.Invoke(this, EventArgs.Empty);
                Close();
                break;
            case Key.Enter when SelectedRegion is not null && _captureDirection is null:
                InstantCaptureRequested?.Invoke(this, new OverlayCaptureRequestedEventArgs(SelectedRegion.Value));
                break;
            case Key.Enter when _captureDirection is not null:
                CaptureCompleted?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void OnPreviewStripDoneClicked(object? sender, EventArgs e)
    {
        CaptureCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSelectionVisuals()
    {
        if (_selectionRect.IsEmpty)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            UpdateShadeRegions();
            return;
        }

        SelectionBorder.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionBorder, _selectionRect.X);
        Canvas.SetTop(SelectionBorder, _selectionRect.Y);
        SelectionBorder.Width = _selectionRect.Width;
        SelectionBorder.Height = _selectionRect.Height;
        UpdateShadeRegions();
    }

    private void UpdateShadeRegions()
    {
        var width = ActualWidth <= 0 ? Width : ActualWidth;
        var height = ActualHeight <= 0 ? Height : ActualHeight;

        if (_selectionRect.IsEmpty)
        {
            SetRectangle(TopShade, 0, 0, width, height);
            SetRectangle(LeftShade, 0, 0, 0, 0);
            SetRectangle(RightShade, 0, 0, 0, 0);
            SetRectangle(BottomShade, 0, 0, 0, 0);
            return;
        }

        SetRectangle(TopShade, 0, 0, width, _selectionRect.Y);
        SetRectangle(LeftShade, 0, _selectionRect.Y, _selectionRect.X, _selectionRect.Height);
        SetRectangle(RightShade, _selectionRect.Right, _selectionRect.Y, width - _selectionRect.Right, _selectionRect.Height);
        SetRectangle(BottomShade, 0, _selectionRect.Bottom, width, height - _selectionRect.Bottom);
    }

    private void PositionPreviewStrip(ScrollDirection direction)
    {
        if (_selectionRect.IsEmpty)
        {
            return;
        }

        var margin = 12d;
        if (direction == ScrollDirection.Vertical)
        {
            Canvas.SetLeft(PreviewStrip, Math.Min(ActualWidth - PreviewStrip.Width - margin, _selectionRect.Right + margin));
            Canvas.SetTop(PreviewStrip, _selectionRect.Top);
        }
        else
        {
            Canvas.SetLeft(PreviewStrip, _selectionRect.Left);
            Canvas.SetTop(PreviewStrip, Math.Min(ActualHeight - PreviewStrip.Height - margin, _selectionRect.Bottom + margin));
        }
    }

    private static void SetRectangle(FrameworkElement rectangle, double x, double y, double width, double height)
    {
        Canvas.SetLeft(rectangle, Math.Max(0, x));
        Canvas.SetTop(rectangle, Math.Max(0, y));
        rectangle.Width = Math.Max(0, width);
        rectangle.Height = Math.Max(0, height);
    }
}
