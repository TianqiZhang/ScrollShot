# Crop-Centric Editor Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the timeline-strip editor with a crop-centric viewport: always-on crop handles, anchor-point zoom, status bar, cut-band secondary tool, and keyboard shortcuts.

**Architecture:** The ImageViewport control gets a major rewrite to support concurrent pan+crop with 8 drag handles and a dim overlay. The PreviewEditorWindow switches from a 3-column (viewport + timeline + inspector) layout to a 3-row (toolbar + viewport + status bar) layout. The PreviewEditorViewModel gets minor cleanup. The TimelineStrip control is deleted. All command/model/compositor code is unchanged.

**Tech Stack:** WPF (.NET 8), XAML, C#, System.Drawing for composition

---

### File Map

| Action | File | Responsibility |
|--------|------|---------------|
| Rewrite | `ScrollShot.Editor/Controls/ImageViewport.xaml` | Viewport with crop handles, dim overlay, cut band overlay |
| Rewrite | `ScrollShot.Editor/Controls/ImageViewport.xaml.cs` | Pan/crop/cut interaction, anchor-point zoom, handle hit-testing |
| Rewrite | `ScrollShot.Editor/PreviewEditorWindow.xaml` | 3-row layout: toolbar, viewport, status bar |
| Rewrite | `ScrollShot.Editor/PreviewEditorWindow.xaml.cs` | Wire keyboard shortcuts, zoom, chrome, cut-band, save/copy |
| Modify | `ScrollShot.Editor/ViewModels/PreviewEditorViewModel.cs` | Expose `IsScrollingCapture`, `Direction` for cut-band; cleanup |
| Delete | `ScrollShot.Editor/Controls/TimelineStrip.xaml` | Removed |
| Delete | `ScrollShot.Editor/Controls/TimelineStrip.xaml.cs` | Removed |

---

### Task 1: Delete TimelineStrip and verify build

**Files:**
- Delete: `ScrollShot/src/ScrollShot.Editor/Controls/TimelineStrip.xaml`
- Delete: `ScrollShot/src/ScrollShot.Editor/Controls/TimelineStrip.xaml.cs`
- Modify: `ScrollShot/src/ScrollShot.Editor/PreviewEditorWindow.xaml`
- Modify: `ScrollShot/src/ScrollShot.Editor/PreviewEditorWindow.xaml.cs`

- [ ] **Step 1: Delete the TimelineStrip files**

```bash
rm ScrollShot/src/ScrollShot.Editor/Controls/TimelineStrip.xaml
rm ScrollShot/src/ScrollShot.Editor/Controls/TimelineStrip.xaml.cs
```

- [ ] **Step 2: Remove TimelineStrip references from PreviewEditorWindow.xaml**

In `PreviewEditorWindow.xaml`, remove the `<controls:TimelineStrip>` element (line 178-181). Also remove the column definition for the timeline (column index 1, `Width="220"`). Set it to `Width="0"`. Remove the `Grid.RowDefinitions` entry for row 5 (the horizontal timeline row, `Height="0"`).

The XAML should keep: header, toolbar, viewport (full span), inspector panel — we'll gut these in later tasks. For now, just remove the timeline reference so the build succeeds.

- [ ] **Step 3: Remove TimelineStrip references from PreviewEditorWindow.xaml.cs**

Remove these lines/blocks:
- `using ScrollShot.Editor.Controls;` (the `TimelineStripEditMode` enum usage) — keep the using if `ImageViewportInteractionMode` is still there
- `_timelineMode` field (line 13)
- `TimelineStripControl.CutRequested -= OnCutRequested;` and `+= OnCutRequested;` (lines 66, 70)
- `TimelineStripControl.TrimChanged -= OnTrimChanged;` and `+= OnTrimChanged;` (lines 67, 71)
- `OnCutRequested` method (lines 205-208)
- `OnTrimChanged` method (lines 210-213)
- `RefreshTimelineState` method (lines 178-192)
- All calls to `RefreshTimelineState()` (in `OnViewModelPropertyChanged` and `RefreshFromViewModel`)
- `SetTimelineMode` method (lines 296-305)
- `OnTrimModeClick` and `OnCutModeClick` methods (lines 250-258)
- All `TimelineStripControl.*` references in `RefreshFromViewModel` (lines 131-156 — timeline visibility and layout logic)
- Remove `TimelineModeHintTextBlock` and `ViewportModeHintTextBlock` references

- [ ] **Step 4: Build and run tests**

```bash
dotnet build ScrollShot/ScrollShot.sln
dotnet test ScrollShot/ScrollShot.sln
```

Expected: Build succeeds. All existing tests pass (none test the TimelineStrip directly).

- [ ] **Step 5: Commit**

```bash
git add -A ScrollShot/src/ScrollShot.Editor/Controls/TimelineStrip.xaml ScrollShot/src/ScrollShot.Editor/Controls/TimelineStrip.xaml.cs ScrollShot/src/ScrollShot.Editor/PreviewEditorWindow.xaml ScrollShot/src/ScrollShot.Editor/PreviewEditorWindow.xaml.cs
git commit -m "Remove TimelineStrip control from editor"
```

---

### Task 2: Rewrite PreviewEditorWindow XAML — three-row layout

**Files:**
- Rewrite: `ScrollShot/src/ScrollShot.Editor/PreviewEditorWindow.xaml`

- [ ] **Step 1: Replace the entire XAML content**

Replace the full content of `PreviewEditorWindow.xaml` with a 3-row grid layout:

```xml
<Window x:Class="ScrollShot.Editor.PreviewEditorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:ScrollShot.Editor.Controls"
        Title="ScrollShot Editor"
        Height="860"
        Width="1360"
        MinHeight="720"
        MinWidth="1120"
        WindowStartupLocation="CenterOwner">
    <Window.InputBindings>
        <KeyBinding Key="Z" Modifiers="Ctrl" Command="{Binding UndoCommand}" />
        <KeyBinding Key="Y" Modifiers="Ctrl" Command="{Binding RedoCommand}" />
        <KeyBinding Key="S" Modifiers="Ctrl" Command="{Binding SaveCommand}" />
        <KeyBinding Key="C" Modifiers="Ctrl" Command="{Binding CopyCommand}" />
    </Window.InputBindings>
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="8" />
            <RowDefinition Height="*" />
            <RowDefinition Height="8" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <!-- Toolbar -->
        <Border Grid.Row="0"
                Style="{StaticResource CardBorderStyle}"
                Padding="14,10">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>

                <!-- Left: Undo/Redo + Chrome -->
                <StackPanel Grid.Column="0"
                            Orientation="Horizontal"
                            VerticalAlignment="Center">
                    <Button Width="72"
                            Margin="0,0,6,0"
                            Content="Undo"
                            Command="{Binding UndoCommand}" />
                    <Button Width="72"
                            Margin="0,0,18,0"
                            Content="Redo"
                            Command="{Binding RedoCommand}" />
                    <CheckBox x:Name="ChromeCheckBox"
                              VerticalAlignment="Center"
                              Margin="0"
                              Content="Window Frame"
                              IsChecked="{Binding CurrentState.IncludeChrome, Mode=OneWay}"
                              Click="OnChromeCheckBoxClick" />
                </StackPanel>

                <!-- Right: Zoom controls -->
                <StackPanel Grid.Column="2"
                            Orientation="Horizontal"
                            VerticalAlignment="Center">
                    <TextBlock VerticalAlignment="Center"
                               Margin="0,0,8,0"
                               Foreground="{DynamicResource SecondaryForegroundBrush}"
                               FontSize="12"
                               FontWeight="SemiBold"
                               Text="Zoom" />
                    <Button Width="36"
                            Margin="0,0,4,0"
                            Content="-"
                            Click="OnZoomOutClick" />
                    <TextBlock x:Name="ZoomTextBlock"
                               Width="56"
                               VerticalAlignment="Center"
                               TextAlignment="Center"
                               FontWeight="SemiBold"
                               FontSize="13"
                               Text="100%" />
                    <Button Width="36"
                            Margin="4,0,4,0"
                            Content="+"
                            Click="OnZoomInClick" />
                    <Button Width="52"
                            Margin="0,0,4,0"
                            Content="Fit"
                            Click="OnFitClick" />
                    <Button Width="52"
                            Content="1:1"
                            Click="OnOneToOneClick" />
                </StackPanel>
            </Grid>
        </Border>

        <!-- Viewport -->
        <controls:ImageViewport x:Name="ViewportControl"
                                Grid.Row="2" />

        <!-- Status Bar -->
        <Border Grid.Row="4"
                Style="{StaticResource CardBorderStyle}"
                Padding="14,10">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="16" />
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>

                <!-- Left: Dimensions -->
                <TextBlock x:Name="DimensionsTextBlock"
                           Grid.Column="0"
                           VerticalAlignment="Center"
                           FontSize="13"
                           FontWeight="SemiBold"
                           Text="0 x 0 px" />

                <!-- Center: Edit summary + Cut Band -->
                <StackPanel Grid.Column="2"
                            Orientation="Horizontal"
                            VerticalAlignment="Center">
                    <TextBlock x:Name="EditSummaryTextBlock"
                               VerticalAlignment="Center"
                               Foreground="{DynamicResource SecondaryForegroundBrush}"
                               FontSize="12"
                               Text="" />
                    <ToggleButton x:Name="CutBandToggleButton"
                                  Margin="16,0,0,0"
                                  Padding="10,6"
                                  Content="Cut Band"
                                  Click="OnCutBandClick"
                                  Visibility="Collapsed" />
                </StackPanel>

                <!-- Right: Save + Copy -->
                <StackPanel Grid.Column="3"
                            Orientation="Horizontal"
                            VerticalAlignment="Center">
                    <Button Margin="0,0,8,0"
                            Content="Copy"
                            Command="{Binding CopyCommand}" />
                    <Button Style="{StaticResource PrimaryButtonStyle}"
                            Content="Save"
                            Command="{Binding SaveCommand}" />
                </StackPanel>
            </Grid>
        </Border>
    </Grid>
</Window>
```

- [ ] **Step 2: Build to verify XAML compiles**

```bash
dotnet build ScrollShot/ScrollShot.sln
```

Expected: Build will fail because the code-behind references elements that no longer exist. That's expected — we fix the code-behind in the next task.

- [ ] **Step 3: Commit XAML separately**

```bash
git add ScrollShot/src/ScrollShot.Editor/PreviewEditorWindow.xaml
git commit -m "Rewrite editor XAML to three-row layout"
```

---

### Task 3: Rewrite PreviewEditorWindow code-behind

**Files:**
- Rewrite: `ScrollShot/src/ScrollShot.Editor/PreviewEditorWindow.xaml.cs`

- [ ] **Step 1: Replace entire code-behind**

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ScrollShot.Editor.Controls;
using ScrollShot.Editor.ViewModels;

namespace ScrollShot.Editor;

public partial class PreviewEditorWindow : Window
{
    private bool _isClosingFromViewModel;
    private bool _hasInitializedViewport;

    public PreviewEditorWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        Closing += OnClosing;
        ViewportControl.ZoomChanged += OnViewportZoomChanged;
        ViewportControl.CropChanged += OnCropChanged;
        ViewportControl.CutRequested += OnCutRequested;
        KeyDown += OnWindowKeyDown;
    }

    public PreviewEditorWindow(PreviewEditorViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    private PreviewEditorViewModel? ViewModel => DataContext as PreviewEditorViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HookViewModel(ViewModel);
        RefreshFromViewModel();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PreviewEditorViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            oldVm.CloseRequested -= OnCloseRequested;
        }

        HookViewModel(e.NewValue as PreviewEditorViewModel);
        RefreshFromViewModel();
    }

    private void HookViewModel(PreviewEditorViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.CloseRequested -= OnCloseRequested;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(PreviewEditorViewModel.CurrentState):
                ViewportControl.SetCrop(ViewModel.CurrentState.CropRect);
                ViewportControl.SetCutBands(ViewModel.CurrentState.CutRanges, ViewModel.Direction);
                break;
            case nameof(PreviewEditorViewModel.PreviewImage):
                RefreshPreviewSurface();
                break;
            case nameof(PreviewEditorViewModel.PreviewSizeText):
                DimensionsTextBlock.Text = ViewModel.PreviewSizeText;
                break;
            case nameof(PreviewEditorViewModel.EditSummary):
                EditSummaryTextBlock.Text = ViewModel.EditSummary;
                break;
            default:
                break;
        }
    }

    private void RefreshFromViewModel()
    {
        if (ViewModel is null)
        {
            return;
        }

        RefreshPreviewSurface();
        ViewportControl.SetCrop(ViewModel.CurrentState.CropRect);
        ViewportControl.SetCutBands(ViewModel.CurrentState.CutRanges, ViewModel.Direction);
        DimensionsTextBlock.Text = ViewModel.PreviewSizeText;
        EditSummaryTextBlock.Text = ViewModel.EditSummary;
        ChromeCheckBox.IsChecked = ViewModel.CurrentState.IncludeChrome;
        CutBandToggleButton.Visibility = ViewModel.IsScrollingCapture ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshPreviewSurface()
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewportControl.SetImage(ViewModel.PreviewImage);
        DimensionsTextBlock.Text = ViewModel.PreviewSizeText;

        if (!_hasInitializedViewport &&
            ViewModel.PreviewImage is not null &&
            ViewportControl.FitToView())
        {
            _hasInitializedViewport = true;
            UpdateZoomText(ViewportControl.ZoomFactor);
        }
    }

    private void OnCropChanged(object? sender, Models.CropRect? e)
    {
        ViewModel?.SetCrop(e);
    }

    private void OnCutRequested(object? sender, Models.CutRange e)
    {
        ViewModel?.AddCut(e);
        CutBandToggleButton.IsChecked = false;
        ViewportControl.SetCutBandMode(false);
    }

    private void OnCloseRequested(object? sender, PreviewEditorCloseRequestedEventArgs e)
    {
        if (e.DiscardConfirmed)
        {
            _isClosingFromViewModel = true;
            Close();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isClosingFromViewModel || ViewModel is null || !ViewModel.HasUnsavedChanges)
        {
            return;
        }

        e.Cancel = true;
        ViewModel.DiscardCommand.Execute(null);
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (ViewportControl.IsCutBandMode)
            {
                CutBandToggleButton.IsChecked = false;
                ViewportControl.SetCutBandMode(false);
            }
            else if (ViewModel?.CurrentState.CropRect is not null)
            {
                ViewModel.SetCrop(null);
            }

            e.Handled = true;
        }
    }

    private void OnChromeCheckBoxClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.ToggleChromeCommand.Execute(null);
    }

    private void OnCutBandClick(object sender, RoutedEventArgs e)
    {
        var isActive = CutBandToggleButton.IsChecked == true;
        ViewportControl.SetCutBandMode(isActive);
    }

    private void OnZoomOutClick(object sender, RoutedEventArgs e) => ViewportControl.ZoomOut();

    private void OnZoomInClick(object sender, RoutedEventArgs e) => ViewportControl.ZoomIn();

    private void OnFitClick(object sender, RoutedEventArgs e) => ViewportControl.FitToView();

    private void OnOneToOneClick(object sender, RoutedEventArgs e) => ViewportControl.SetOneToOne();

    private void OnViewportZoomChanged(object? sender, double zoomFactor) => UpdateZoomText(zoomFactor);

    private void UpdateZoomText(double zoomFactor)
    {
        ZoomTextBlock.Text = $"{(int)(zoomFactor * 100)}%";
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build ScrollShot/ScrollShot.sln
```

Expected: Build will fail because `ImageViewport` doesn't yet have `SetCutBands`, `SetCutBandMode`, `IsCutBandMode`, or `CutRequested`. That's expected — we add those in the next tasks. For now, commit the code-behind.

- [ ] **Step 3: Commit**

```bash
git add ScrollShot/src/ScrollShot.Editor/PreviewEditorWindow.xaml.cs
git commit -m "Rewrite editor code-behind for crop-centric layout"
```

---

### Task 4: Rewrite ImageViewport XAML — crop handles and dim overlay

**Files:**
- Rewrite: `ScrollShot/src/ScrollShot.Editor/Controls/ImageViewport.xaml`

- [ ] **Step 1: Replace entire ImageViewport XAML**

```xml
<UserControl x:Class="ScrollShot.Editor.Controls.ImageViewport"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             d:DesignHeight="450"
             d:DesignWidth="800"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d">
    <Border Background="{DynamicResource CardMutedBackgroundBrush}"
            BorderBrush="{DynamicResource BorderBrush}"
            BorderThickness="1"
            CornerRadius="12">
        <ScrollViewer x:Name="ScrollViewer"
                       HorizontalScrollBarVisibility="Auto"
                       VerticalScrollBarVisibility="Auto"
                       PreviewMouseWheel="OnPreviewMouseWheel"
                       MouseLeftButtonDown="OnMouseLeftButtonDown"
                       MouseLeftButtonUp="OnMouseLeftButtonUp"
                       MouseMove="OnMouseMove"
                       MouseRightButtonDown="OnMouseRightButtonDown"
                       MouseRightButtonUp="OnMouseRightButtonUp">
            <Grid>
                <Image x:Name="ViewportImage"
                       Stretch="None"
                       RenderTransformOrigin="0,0">
                    <Image.RenderTransform>
                        <ScaleTransform x:Name="ScaleTransform"
                                        ScaleX="1"
                                        ScaleY="1" />
                    </Image.RenderTransform>
                </Image>
                <Canvas x:Name="OverlayCanvas"
                        Background="Transparent"
                        ClipToBounds="True">
                    <!-- Dim overlay: 4 rectangles around crop -->
                    <Rectangle x:Name="DimTop" Fill="#66000000" Visibility="Collapsed" />
                    <Rectangle x:Name="DimBottom" Fill="#66000000" Visibility="Collapsed" />
                    <Rectangle x:Name="DimLeft" Fill="#66000000" Visibility="Collapsed" />
                    <Rectangle x:Name="DimRight" Fill="#66000000" Visibility="Collapsed" />

                    <!-- Crop border -->
                    <Rectangle x:Name="CropBorder"
                               Stroke="{DynamicResource AccentBrush}"
                               StrokeThickness="2"
                               Fill="Transparent"
                               Visibility="Collapsed" />

                    <!-- 8 crop handles -->
                    <Rectangle x:Name="HandleTL" Width="10" Height="10" Fill="{DynamicResource AccentBrush}" Visibility="Collapsed" />
                    <Rectangle x:Name="HandleTC" Width="10" Height="10" Fill="{DynamicResource AccentBrush}" Visibility="Collapsed" />
                    <Rectangle x:Name="HandleTR" Width="10" Height="10" Fill="{DynamicResource AccentBrush}" Visibility="Collapsed" />
                    <Rectangle x:Name="HandleML" Width="10" Height="10" Fill="{DynamicResource AccentBrush}" Visibility="Collapsed" />
                    <Rectangle x:Name="HandleMR" Width="10" Height="10" Fill="{DynamicResource AccentBrush}" Visibility="Collapsed" />
                    <Rectangle x:Name="HandleBL" Width="10" Height="10" Fill="{DynamicResource AccentBrush}" Visibility="Collapsed" />
                    <Rectangle x:Name="HandleBC" Width="10" Height="10" Fill="{DynamicResource AccentBrush}" Visibility="Collapsed" />
                    <Rectangle x:Name="HandleBR" Width="10" Height="10" Fill="{DynamicResource AccentBrush}" Visibility="Collapsed" />

                    <!-- Cut band overlays (added programmatically) -->
                    <!-- Cut band preview during drag -->
                    <Rectangle x:Name="CutBandPreview"
                               Fill="#50DC3545"
                               Visibility="Collapsed" />
                </Canvas>
            </Grid>
        </ScrollViewer>
    </Border>
</UserControl>
```

- [ ] **Step 2: Commit XAML**

```bash
git add ScrollShot/src/ScrollShot.Editor/Controls/ImageViewport.xaml
git commit -m "Rewrite viewport XAML with crop handles and dim overlay"
```

---

### Task 5: Rewrite ImageViewport code-behind — crop handles, anchor zoom, cut band

**Files:**
- Rewrite: `ScrollShot/src/ScrollShot.Editor/Controls/ImageViewport.xaml.cs`

This is the largest task. The code-behind implements:
1. Always-on crop with 8 resizable handles
2. Anchor-point zoom (zoom toward cursor)
3. Pan via left-drag (outside crop), right-drag, or middle-drag
4. Crop repositioning via left-drag inside crop
5. Cut band mode as a secondary tool
6. Dim overlay rendering

- [ ] **Step 1: Replace the entire code-behind**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private const double MinCropPixels = 2;

    private BitmapSource? _image;
    private CropRect? _cropRect;
    private bool _isCutBandMode;

    // Drag state
    private DragAction _currentDrag = DragAction.None;
    private Point _dragStartViewport;
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
        _cropRect = cropRect;
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

        if (_image is null) return;

        foreach (var cut in cutRanges)
        {
            var rect = new Rectangle
            {
                Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(120, 220, 53, 69)),
                IsHitTestVisible = false,
            };

            if (direction == ScrollDirection.Vertical)
            {
                Canvas.SetLeft(rect, 0);
                Canvas.SetTop(rect, cut.StartPixel);
                rect.Width = _image.PixelWidth;
                rect.Height = cut.EndPixel - cut.StartPixel;
            }
            else
            {
                Canvas.SetLeft(rect, cut.StartPixel);
                Canvas.SetTop(rect, 0);
                rect.Width = cut.EndPixel - cut.StartPixel;
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
        _dragStartViewport = e.GetPosition(this);
        _dragStartImage = e.GetPosition(OverlayCanvas);

        if (_isCutBandMode)
        {
            _cutBandStart = _dragStartImage;
            CutBandPreview.Visibility = Visibility.Visible;
            UpdateCutBandPreview(_dragStartImage, _dragStartImage);
            CaptureMouse();
            return;
        }

        if (_cropRect is { } crop)
        {
            var handle = HitTestHandle(_dragStartImage, crop);
            if (handle != DragAction.None)
            {
                _currentDrag = handle;
                _dragStartCrop = crop;
                CaptureMouse();
                return;
            }

            if (IsInsideCrop(_dragStartImage, crop))
            {
                _currentDrag = DragAction.MoveCrop;
                _dragStartCrop = crop;
                CaptureMouse();
                return;
            }
        }

        // Outside crop (or no crop): start new crop
        _currentDrag = DragAction.NewCrop;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isCutBandMode && _cutBandStart is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateCutBandPreview(_cutBandStart.Value, e.GetPosition(OverlayCanvas));
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

        var imagePos = e.GetPosition(OverlayCanvas);

        switch (_currentDrag)
        {
            case DragAction.NewCrop:
                var newRect = MakeRect(_dragStartImage, imagePos);
                if (newRect.Width >= MinCropPixels && newRect.Height >= MinCropPixels)
                {
                    _cropRect = new CropRect((int)newRect.X, (int)newRect.Y, (int)newRect.Width, (int)newRect.Height);
                    UpdateCropVisuals();
                }
                break;

            case DragAction.MoveCrop when _dragStartCrop is { } startCrop:
                var dx = (int)(imagePos.X - _dragStartImage.X);
                var dy = (int)(imagePos.Y - _dragStartImage.Y);
                _cropRect = new CropRect(startCrop.X + dx, startCrop.Y + dy, startCrop.Width, startCrop.Height);
                UpdateCropVisuals();
                break;

            default:
                if (_currentDrag >= DragAction.ResizeTL && _dragStartCrop is { } sc)
                {
                    _cropRect = ResizeCrop(sc, _currentDrag, imagePos);
                    UpdateCropVisuals();
                }
                break;
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isCutBandMode && _cutBandStart is not null)
        {
            var endPos = e.GetPosition(OverlayCanvas);
            CutBandPreview.Visibility = Visibility.Collapsed;
            EmitCutBand(_cutBandStart.Value, endPos);
            _cutBandStart = null;
            ReleaseMouseCapture();
            return;
        }

        if (_currentDrag != DragAction.None)
        {
            if (_currentDrag == DragAction.NewCrop)
            {
                var finalPos = e.GetPosition(OverlayCanvas);
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
            ReleaseMouseCapture();
        }
    }

    // --- Right-click pan ---

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _rightPanStart = e.GetPosition(this);
        CaptureMouse();
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _rightPanStart = null;
        ReleaseMouseCapture();
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
            foreach (var h in handles) h.Visibility = Visibility.Collapsed;
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

    private DragAction HitTestHandle(Point imagePos, CropRect crop)
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

    private static CropRect ResizeCrop(CropRect start, DragAction handle, Point imagePos)
    {
        var left = start.X;
        var top = start.Y;
        var right = start.X + start.Width;
        var bottom = start.Y + start.Height;

        switch (handle)
        {
            case DragAction.ResizeTL: left = (int)imagePos.X; top = (int)imagePos.Y; break;
            case DragAction.ResizeTC: top = (int)imagePos.Y; break;
            case DragAction.ResizeTR: right = (int)imagePos.X; top = (int)imagePos.Y; break;
            case DragAction.ResizeML: left = (int)imagePos.X; break;
            case DragAction.ResizeMR: right = (int)imagePos.X; break;
            case DragAction.ResizeBL: left = (int)imagePos.X; bottom = (int)imagePos.Y; break;
            case DragAction.ResizeBC: bottom = (int)imagePos.Y; break;
            case DragAction.ResizeBR: right = (int)imagePos.X; bottom = (int)imagePos.Y; break;
        }

        // Normalize in case user dragged past opposite edge
        var x = Math.Min(left, right);
        var y = Math.Min(top, bottom);
        var w = Math.Max(1, Math.Abs(right - left));
        var h = Math.Max(1, Math.Abs(bottom - top));

        return new CropRect(x, y, w, h);
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
        if (_image is null) return;

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
        int s, e2;
        if (_cutDirection == ScrollDirection.Vertical)
        {
            s = (int)Math.Min(start.Y, end.Y);
            e2 = (int)Math.Max(start.Y, end.Y);
        }
        else
        {
            s = (int)Math.Min(start.X, end.X);
            e2 = (int)Math.Max(start.X, end.X);
        }

        if (e2 - s > 1)
        {
            CutRequested?.Invoke(this, new CutRange(Math.Max(0, s), e2));
        }
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
        ResizeTL, ResizeTC, ResizeTR,
        ResizeML, ResizeMR,
        ResizeBL, ResizeBC, ResizeBR,
    }
}
```

- [ ] **Step 2: Build and run tests**

```bash
dotnet build ScrollShot/ScrollShot.sln
dotnet test ScrollShot/ScrollShot.sln
```

Expected: Build succeeds. All tests pass.

- [ ] **Step 3: Commit**

```bash
git add ScrollShot/src/ScrollShot.Editor/Controls/ImageViewport.xaml ScrollShot/src/ScrollShot.Editor/Controls/ImageViewport.xaml.cs
git commit -m "Rewrite viewport with crop handles, anchor zoom, cut band"
```

---

### Task 6: Update PreviewEditorViewModel — expose IsScrollingCapture, cleanup

**Files:**
- Modify: `ScrollShot/src/ScrollShot.Editor/ViewModels/PreviewEditorViewModel.cs`

- [ ] **Step 1: Add IsScrollingCapture property and update EditSummary default text**

In `PreviewEditorViewModel.cs`:

Add a public property:
```csharp
public bool IsScrollingCapture => _captureResult.IsScrollingCapture;
```

Update `BuildEditSummary` to change the default text when no edits:
```csharp
return parts.Count == 0
    ? ""
    : string.Join(" \u00b7 ", parts);
```

(Empty string instead of instructional text — the status bar doesn't need a paragraph.)

- [ ] **Step 2: Build and run tests**

```bash
dotnet build ScrollShot/ScrollShot.sln
dotnet test ScrollShot/ScrollShot.sln
```

Expected: Build succeeds. All tests pass. The test at line 45 checks `EditSummary` indirectly through `BuildEditSummary`, and the default state tests don't assert on the default text content.

- [ ] **Step 3: Commit**

```bash
git add ScrollShot/src/ScrollShot.Editor/ViewModels/PreviewEditorViewModel.cs
git commit -m "Expose IsScrollingCapture, simplify empty edit summary"
```

---

### Task 7: Verify full build and all tests

**Files:** None (verification only)

- [ ] **Step 1: Clean build**

```bash
dotnet build ScrollShot/ScrollShot.sln
```

Expected: 0 errors, 0 warnings (or pre-existing warnings only).

- [ ] **Step 2: Run all tests**

```bash
dotnet test ScrollShot/ScrollShot.sln
```

Expected: All tests pass. No test references TimelineStrip or any removed type.

- [ ] **Step 3: Verify no stale references**

```bash
grep -r "TimelineStrip" ScrollShot/src/ --include="*.cs" --include="*.xaml"
```

Expected: No matches.

- [ ] **Step 4: Commit any fixups if needed, then tag**

If any fixups were needed, commit them. Otherwise, this task is just a verification checkpoint.
