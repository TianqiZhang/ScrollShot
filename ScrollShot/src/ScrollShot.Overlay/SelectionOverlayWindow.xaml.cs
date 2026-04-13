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
    private IntPtr _windowHandle;

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

    public bool IsExcludedFromCapture { get; private set; }

    public void UpdatePreview(Bitmap previewBitmap)
    {
        if (_captureDirection is null)
        {
            return;
        }

        using (previewBitmap)
        {
            Dispatcher.Invoke(() =>
            {
                PreviewStrip.SetPreview(previewBitmap, _captureDirection.Value);
                PositionPreviewStrip(_captureDirection.Value);
            });
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var virtualBounds = ScreenHelper.GetVirtualScreenBounds();
        Left = virtualBounds.Left;
        Top = virtualBounds.Top;
        Width = virtualBounds.Width;
        Height = virtualBounds.Height;
        UpdateShadeRegions();
        CenterInstruction();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is not HwndSource source)
        {
            return;
        }

        _windowHandle = source.Handle;
        var style = WindowStyles.GetWindowLongPtr(_windowHandle, WindowStyles.GwlExStyle);
        WindowStyles.SetWindowLongPtr(_windowHandle, WindowStyles.GwlExStyle, style | WindowStyles.WsExToolWindow);
        IsExcludedFromCapture = WindowStyles.SetWindowDisplayAffinity(_windowHandle, WindowStyles.WdaExcludeFromCapture);
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_captureDirection is not null)
        {
            return;
        }

        _selectionStart = e.GetPosition(RootCanvas);
        _selectionRect = Rect.Empty;
        _captureDirection = null;
        PreviewStrip.Visibility = Visibility.Collapsed;
        InstructionBorder.Visibility = Visibility.Collapsed;
        CaptureMouse();
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_captureDirection is not null)
        {
            return;
        }

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
        if (_captureDirection is not null)
        {
            return;
        }

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
            InstructionBorder.Visibility = Visibility.Visible;
            UpdateSelectionVisuals();
            CenterInstruction();
            return;
        }

        SelectedRegion = ScreenHelper.ToPhysicalScreenRect(_selectionRect, this);
        InstructionBorder.Visibility = Visibility.Visible;
        CenterInstruction();
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (SelectedRegion is null)
        {
            return;
        }

        if (_captureDirection is null)
        {
            StartScrollCapture(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                ? ScrollDirection.Horizontal
                : ScrollDirection.Vertical);
        }

        ForwardWheelInput(e);
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
                RequestInstantCapture();
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
        var availableWidth = ActualWidth <= 0 ? Width : ActualWidth;
        var availableHeight = ActualHeight <= 0 ? Height : ActualHeight;
        var previewWidth = GetPreviewWidth(direction);
        var previewHeight = GetPreviewHeight(direction);

        if (direction == ScrollDirection.Vertical)
        {
            var rightX = _selectionRect.Right + margin;
            var leftX = _selectionRect.Left - previewWidth - margin;
            var x = rightX + previewWidth <= availableWidth
                ? rightX
                : Math.Max(margin, leftX);
            var y = Math.Clamp(_selectionRect.Top, margin, Math.Max(margin, availableHeight - previewHeight - margin));

            Canvas.SetLeft(PreviewStrip, x);
            Canvas.SetTop(PreviewStrip, y);
        }
        else
        {
            var belowY = _selectionRect.Bottom + margin;
            var aboveY = _selectionRect.Top - previewHeight - margin;
            var y = belowY + previewHeight <= availableHeight
                ? belowY
                : Math.Max(margin, aboveY);
            var x = Math.Clamp(_selectionRect.Left, margin, Math.Max(margin, availableWidth - previewWidth - margin));

            Canvas.SetLeft(PreviewStrip, x);
            Canvas.SetTop(PreviewStrip, y);
        }
    }

    private static void SetRectangle(FrameworkElement rectangle, double x, double y, double width, double height)
    {
        Canvas.SetLeft(rectangle, Math.Max(0, x));
        Canvas.SetTop(rectangle, Math.Max(0, y));
        rectangle.Width = Math.Max(0, width);
        rectangle.Height = Math.Max(0, height);
    }

    private void CenterInstruction()
    {
        InstructionBorder.UpdateLayout();
        var w = ActualWidth <= 0 ? Width : ActualWidth;
        var h = ActualHeight <= 0 ? Height : ActualHeight;
        Canvas.SetLeft(InstructionBorder, (w - InstructionBorder.ActualWidth) / 2);
        Canvas.SetTop(InstructionBorder, (h - InstructionBorder.ActualHeight) / 2);
    }

    private void RequestInstantCapture()
    {
        if (SelectedRegion is not ScreenRect region)
        {
            return;
        }

        InstantCaptureRequested?.Invoke(this, new OverlayCaptureRequestedEventArgs(region));
    }

    private void StartScrollCapture(ScrollDirection direction)
    {
        if (SelectedRegion is not ScreenRect region || _captureDirection is not null)
        {
            return;
        }

        _captureDirection = direction;
        InstructionBorder.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Collapsed;
        PreviewStrip.Visibility = Visibility.Visible;
        PositionPreviewStrip(direction);
        ScrollCaptureStarted?.Invoke(this, new OverlayCaptureRequestedEventArgs(region, direction));
    }

    private void ForwardWheelInput(MouseWheelEventArgs e)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        SetOverlayTransparency(true);
        try
        {
            if (!WindowStyles.GetCursorPos(out var cursorPosition))
            {
                return;
            }

            var targetWindow = WindowStyles.WindowFromPoint(cursorPosition);
            if (targetWindow == IntPtr.Zero || targetWindow == _windowHandle)
            {
                return;
            }

            var message = _captureDirection == ScrollDirection.Horizontal
                ? WindowStyles.WmMouseHWheel
                : WindowStyles.WmMouseWheel;
            var keyState = GetMouseWheelKeyState();
            var wParam = new IntPtr(((short)e.Delta << 16) | keyState);
            var lParam = new IntPtr(((cursorPosition.Y & 0xFFFF) << 16) | (cursorPosition.X & 0xFFFF));
            WindowStyles.SendMessage(targetWindow, message, wParam, lParam);
        }
        finally
        {
            SetOverlayTransparency(false);
        }
    }

    private void SetOverlayTransparency(bool enabled)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        var style = WindowStyles.GetWindowLongPtr(_windowHandle, WindowStyles.GwlExStyle).ToInt64();
        style = enabled
            ? style | WindowStyles.WsExTransparent
            : style & ~WindowStyles.WsExTransparent;

        WindowStyles.SetWindowLongPtr(_windowHandle, WindowStyles.GwlExStyle, new IntPtr(style));
        WindowStyles.SetWindowPos(
            _windowHandle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            WindowStyles.SwpNoMove | WindowStyles.SwpNoSize | WindowStyles.SwpNoZOrder | WindowStyles.SwpNoActivate | WindowStyles.SwpFrameChanged);
    }

    private static int GetMouseWheelKeyState()
    {
        var keyState = 0;
        if (Mouse.LeftButton == MouseButtonState.Pressed)
        {
            keyState |= 0x0001;
        }

        if (Mouse.RightButton == MouseButtonState.Pressed)
        {
            keyState |= 0x0002;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            keyState |= 0x0004;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            keyState |= 0x0008;
        }

        if (Mouse.MiddleButton == MouseButtonState.Pressed)
        {
            keyState |= 0x0010;
        }

        return keyState;
    }

    private double GetPreviewWidth(ScrollDirection direction)
    {
        var width = PreviewStrip.ActualWidth > 0 ? PreviewStrip.ActualWidth : PreviewStrip.Width;
        if (double.IsNaN(width) || width <= 0)
        {
            width = direction == ScrollDirection.Vertical ? 260d : 420d;
        }

        return width;
    }

    private double GetPreviewHeight(ScrollDirection direction)
    {
        var height = PreviewStrip.ActualHeight > 0 ? PreviewStrip.ActualHeight : PreviewStrip.Height;
        if (double.IsNaN(height) || height <= 0)
        {
            height = direction == ScrollDirection.Vertical ? 420d : 250d;
        }

        return height;
    }
}
