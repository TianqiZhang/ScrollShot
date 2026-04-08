using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ScrollShot.Editor.Models;
using ScrollShot.Scroll.Models;
using Point = System.Windows.Point;

namespace ScrollShot.Editor.Controls;

public partial class TimelineStrip : UserControl
{
    private Point? _dragStart;
    private bool _headTrimDrag;
    private bool _tailTrimDrag;

    public TimelineStrip()
    {
        InitializeComponent();
        SizeChanged += (_, _) => RenderOverlay();
    }

    public event EventHandler<CutRange>? CutRequested;

    public event EventHandler<TrimRange>? TrimChanged;

    public ScrollDirection Direction { get; private set; } = ScrollDirection.Vertical;

    public int PrimaryAxisLength { get; private set; }

    public TrimRange TrimRange { get; private set; } = new(0, 0);

    public IReadOnlyList<CutRange> CutRanges { get; private set; } = Array.Empty<CutRange>();

    public void SetState(BitmapSource? thumbnail, ScrollDirection direction, int primaryAxisLength, TrimRange trimRange, IReadOnlyList<CutRange> cutRanges)
    {
        ThumbnailImage.Source = thumbnail;
        Direction = direction;
        PrimaryAxisLength = Math.Max(1, primaryAxisLength);
        TrimRange = trimRange;
        CutRanges = cutRanges;
        Width = direction == ScrollDirection.Vertical ? 180 : double.NaN;
        Height = direction == ScrollDirection.Vertical ? double.NaN : 140;
        RenderOverlay();
    }

    private void OnOverlayMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(OverlayCanvas);
        var visualPosition = GetPrimaryVisualCoordinate(_dragStart.Value);
        var headHandle = MapPixelToVisual(TrimRange.HeadTrimPixels);
        var tailHandle = MapPixelToVisual(PrimaryAxisLength - TrimRange.TailTrimPixels);
        _headTrimDrag = Math.Abs(visualPosition - headHandle) <= 8;
        _tailTrimDrag = Math.Abs(visualPosition - tailHandle) <= 8;
        OverlayCanvas.CaptureMouse();
    }

    private void OnOverlayMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPixel = MapVisualToPixel(GetPrimaryVisualCoordinate(e.GetPosition(OverlayCanvas)));
        if (_headTrimDrag)
        {
            TrimChanged?.Invoke(this, new TrimRange(Math.Clamp(currentPixel, 0, PrimaryAxisLength - TrimRange.TailTrimPixels - 1), TrimRange.TailTrimPixels));
        }
        else if (_tailTrimDrag)
        {
            var tail = Math.Clamp(PrimaryAxisLength - currentPixel, 0, PrimaryAxisLength - TrimRange.HeadTrimPixels - 1);
            TrimChanged?.Invoke(this, new TrimRange(TrimRange.HeadTrimPixels, tail));
        }
    }

    private void OnOverlayMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }

        var dragEnd = e.GetPosition(OverlayCanvas);
        if (!_headTrimDrag && !_tailTrimDrag)
        {
            var start = MapVisualToPixel(GetPrimaryVisualCoordinate(_dragStart.Value));
            var end = MapVisualToPixel(GetPrimaryVisualCoordinate(dragEnd));
            if (Math.Abs(end - start) > 1)
            {
                CutRequested?.Invoke(this, new CutRange(Math.Min(start, end), Math.Max(start, end)));
            }
        }

        _dragStart = null;
        _headTrimDrag = false;
        _tailTrimDrag = false;
        OverlayCanvas.ReleaseMouseCapture();
    }

    private void RenderOverlay()
    {
        if (PrimaryAxisLength <= 0 || OverlayCanvas.ActualWidth <= 0 || OverlayCanvas.ActualHeight <= 0)
        {
            return;
        }

        OverlayCanvas.Children.Clear();

        var headHandle = CreateHandle(MapPixelToVisual(TrimRange.HeadTrimPixels));
        var tailHandle = CreateHandle(MapPixelToVisual(PrimaryAxisLength - TrimRange.TailTrimPixels));

        OverlayCanvas.Children.Add(headHandle);
        OverlayCanvas.Children.Add(tailHandle);

        foreach (var cutRange in CutRanges)
        {
            var start = MapPixelToVisual(cutRange.StartPixel);
            var end = MapPixelToVisual(cutRange.EndPixel);
            var band = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(120, 220, 53, 69)),
                Width = Direction == ScrollDirection.Vertical ? OverlayCanvas.ActualWidth : Math.Max(2, end - start),
                Height = Direction == ScrollDirection.Vertical ? Math.Max(2, end - start) : OverlayCanvas.ActualHeight,
            };

            Canvas.SetLeft(band, Direction == ScrollDirection.Vertical ? 0 : start);
            Canvas.SetTop(band, Direction == ScrollDirection.Vertical ? start : 0);
            OverlayCanvas.Children.Add(band);
        }
    }

    private Rectangle CreateHandle(double position)
    {
        var handle = new Rectangle
        {
            Fill = Brushes.Orange,
            Width = Direction == ScrollDirection.Vertical ? OverlayCanvas.ActualWidth : 6,
            Height = Direction == ScrollDirection.Vertical ? 6 : OverlayCanvas.ActualHeight,
        };

        Canvas.SetLeft(handle, Direction == ScrollDirection.Vertical ? 0 : position - 3);
        Canvas.SetTop(handle, Direction == ScrollDirection.Vertical ? position - 3 : 0);
        return handle;
    }

    private double MapPixelToVisual(int pixelOffset)
    {
        var visualLength = Direction == ScrollDirection.Vertical ? OverlayCanvas.ActualHeight : OverlayCanvas.ActualWidth;
        return visualLength * pixelOffset / PrimaryAxisLength;
    }

    private int MapVisualToPixel(double visualOffset)
    {
        var visualLength = Direction == ScrollDirection.Vertical ? OverlayCanvas.ActualHeight : OverlayCanvas.ActualWidth;
        return (int)Math.Round(Math.Clamp(visualOffset / Math.Max(1, visualLength), 0, 1) * PrimaryAxisLength);
    }

    private double GetPrimaryVisualCoordinate(Point point)
    {
        return Direction == ScrollDirection.Vertical ? point.Y : point.X;
    }
}
