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

public enum TimelineStripEditMode
{
    Trim,
    Cut,
}

public partial class TimelineStrip : UserControl
{
    private Point? _dragStart;
    private Point? _dragCurrent;
    private bool _headTrimDrag;
    private bool _tailTrimDrag;
    private TimelineStripEditMode _editMode = TimelineStripEditMode.Trim;

    public TimelineStrip()
    {
        InitializeComponent();
        SizeChanged += (_, _) => RenderOverlay();
        UpdateModeVisuals();
    }

    public event EventHandler<CutRange>? CutRequested;

    public event EventHandler<TrimRange>? TrimChanged;

    public ScrollDirection Direction { get; private set; } = ScrollDirection.Vertical;

    public int PrimaryAxisLength { get; private set; }

    public TrimRange TrimRange { get; private set; } = new(0, 0);

    public IReadOnlyList<CutRange> CutRanges { get; private set; } = Array.Empty<CutRange>();

    public void SetEditMode(TimelineStripEditMode editMode)
    {
        _editMode = editMode;
        UpdateModeVisuals();
        RenderOverlay();
    }

    public void SetState(BitmapSource? thumbnail, ScrollDirection direction, int primaryAxisLength, TrimRange trimRange, IReadOnlyList<CutRange> cutRanges)
    {
        ThumbnailImage.Source = thumbnail;
        Direction = direction;
        PrimaryAxisLength = Math.Max(1, primaryAxisLength);
        TrimRange = trimRange;
        CutRanges = cutRanges;
        Width = direction == ScrollDirection.Vertical ? 168 : double.NaN;
        Height = direction == ScrollDirection.Vertical ? double.NaN : 128;
        RenderOverlay();
    }

    private void OnOverlayMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(OverlayCanvas);
        _dragCurrent = _dragStart;
        var visualPosition = GetPrimaryVisualCoordinate(_dragStart.Value);
        if (_editMode == TimelineStripEditMode.Trim)
        {
            var headHandle = MapPixelToVisual(TrimRange.HeadTrimPixels);
            var tailHandle = MapPixelToVisual(PrimaryAxisLength - TrimRange.TailTrimPixels);
            _headTrimDrag = Math.Abs(visualPosition - headHandle) <= 10;
            _tailTrimDrag = Math.Abs(visualPosition - tailHandle) <= 10;
        }

        OverlayCanvas.CaptureMouse();
    }

    private void OnOverlayMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _dragCurrent = e.GetPosition(OverlayCanvas);
        var currentPixel = MapVisualToPixel(GetPrimaryVisualCoordinate(e.GetPosition(OverlayCanvas)));
        if (_editMode == TimelineStripEditMode.Trim && _headTrimDrag)
        {
            TrimChanged?.Invoke(this, new TrimRange(Math.Clamp(currentPixel, 0, PrimaryAxisLength - TrimRange.TailTrimPixels - 1), TrimRange.TailTrimPixels));
        }
        else if (_editMode == TimelineStripEditMode.Trim && _tailTrimDrag)
        {
            var tail = Math.Clamp(PrimaryAxisLength - currentPixel, 0, PrimaryAxisLength - TrimRange.HeadTrimPixels - 1);
            TrimChanged?.Invoke(this, new TrimRange(TrimRange.HeadTrimPixels, tail));
        }

        RenderOverlay();
    }

    private void OnOverlayMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }

        var dragEnd = e.GetPosition(OverlayCanvas);
        if (_editMode == TimelineStripEditMode.Cut)
        {
            var start = MapVisualToPixel(GetPrimaryVisualCoordinate(_dragStart.Value));
            var end = MapVisualToPixel(GetPrimaryVisualCoordinate(dragEnd));
            if (Math.Abs(end - start) > 1)
            {
                CutRequested?.Invoke(this, new CutRange(Math.Min(start, end), Math.Max(start, end)));
            }
        }

        _dragStart = null;
        _dragCurrent = null;
        _headTrimDrag = false;
        _tailTrimDrag = false;
        OverlayCanvas.ReleaseMouseCapture();
        RenderOverlay();
    }

    private void RenderOverlay()
    {
        if (PrimaryAxisLength <= 0 || OverlayCanvas.ActualWidth <= 0 || OverlayCanvas.ActualHeight <= 0)
        {
            return;
        }

        OverlayCanvas.Children.Clear();

        if (_editMode == TimelineStripEditMode.Trim)
        {
            AddTrimRegions();

            var headHandlePosition = MapPixelToVisual(TrimRange.HeadTrimPixels);
            var tailHandlePosition = MapPixelToVisual(PrimaryAxisLength - TrimRange.TailTrimPixels);
            OverlayCanvas.Children.Add(CreateHandle(headHandlePosition));
            OverlayCanvas.Children.Add(CreateHandle(tailHandlePosition));
            OverlayCanvas.Children.Add(CreateLabel("Start", headHandlePosition));
            OverlayCanvas.Children.Add(CreateLabel("End", tailHandlePosition));
        }

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
            OverlayCanvas.Children.Add(CreateLabel("Cut", (start + end) / 2));
        }

        if (_editMode == TimelineStripEditMode.Cut && _dragStart is not null && _dragCurrent is not null)
        {
            AddSelectionPreview(_dragStart.Value, _dragCurrent.Value);
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

    private void AddTrimRegions()
    {
        if (TrimRange.HeadTrimPixels > 0)
        {
            AddMutedBand(0, MapPixelToVisual(TrimRange.HeadTrimPixels));
        }

        if (TrimRange.TailTrimPixels > 0)
        {
            var start = MapPixelToVisual(PrimaryAxisLength - TrimRange.TailTrimPixels);
            var end = MapPixelToVisual(PrimaryAxisLength);
            AddMutedBand(start, end);
        }
    }

    private void AddMutedBand(double start, double end)
    {
        var band = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(120, 15, 23, 42)),
            Width = Direction == ScrollDirection.Vertical ? OverlayCanvas.ActualWidth : Math.Max(2, end - start),
            Height = Direction == ScrollDirection.Vertical ? Math.Max(2, end - start) : OverlayCanvas.ActualHeight,
        };

        Canvas.SetLeft(band, Direction == ScrollDirection.Vertical ? 0 : start);
        Canvas.SetTop(band, Direction == ScrollDirection.Vertical ? start : 0);
        OverlayCanvas.Children.Add(band);
    }

    private void AddSelectionPreview(Point startPoint, Point endPoint)
    {
        var start = MapPixelToVisual(MapVisualToPixel(GetPrimaryVisualCoordinate(startPoint)));
        var end = MapPixelToVisual(MapVisualToPixel(GetPrimaryVisualCoordinate(endPoint)));
        var min = Math.Min(start, end);
        var max = Math.Max(start, end);
        var band = new Rectangle
        {
            Stroke = Brushes.DeepSkyBlue,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(90, 96, 165, 250)),
            Width = Direction == ScrollDirection.Vertical ? OverlayCanvas.ActualWidth : Math.Max(2, max - min),
            Height = Direction == ScrollDirection.Vertical ? Math.Max(2, max - min) : OverlayCanvas.ActualHeight,
        };

        Canvas.SetLeft(band, Direction == ScrollDirection.Vertical ? 0 : min);
        Canvas.SetTop(band, Direction == ScrollDirection.Vertical ? min : 0);
        OverlayCanvas.Children.Add(band);
    }

    private TextBlock CreateLabel(string text, double position)
    {
        var label = new TextBlock
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 11, 18, 32)),
            Foreground = Brushes.White,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(6, 2, 6, 2),
            Text = text,
        };

        if (Direction == ScrollDirection.Vertical)
        {
            Canvas.SetLeft(label, 8);
            Canvas.SetTop(label, Math.Clamp(position + 6, 0, Math.Max(0, OverlayCanvas.ActualHeight - 24)));
        }
        else
        {
            Canvas.SetLeft(label, Math.Clamp(position + 6, 0, Math.Max(0, OverlayCanvas.ActualWidth - 60)));
            Canvas.SetTop(label, 8);
        }

        return label;
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

    private void UpdateModeVisuals()
    {
        if (_editMode == TimelineStripEditMode.Trim)
        {
            ModeTextBlock.Text = "Trim ends";
            HintTextBlock.Text = "Drag the top and bottom handles to shorten the capture.";
            OverlayCanvas.Cursor = Direction == ScrollDirection.Vertical ? Cursors.SizeNS : Cursors.SizeWE;
            return;
        }

        ModeTextBlock.Text = "Remove part";
        HintTextBlock.Text = "Drag across the strip to remove part of the image.";
        OverlayCanvas.Cursor = Cursors.Cross;
    }
}
