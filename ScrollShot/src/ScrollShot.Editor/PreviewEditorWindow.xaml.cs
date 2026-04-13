using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScrollShot.Editor.Controls;
using ScrollShot.Editor.ViewModels;

namespace ScrollShot.Editor;

public partial class PreviewEditorWindow : Window
{
    private ImageViewportInteractionMode _viewportMode = ImageViewportInteractionMode.Pan;
    private TimelineStripEditMode _timelineMode = TimelineStripEditMode.Trim;
    private bool _isClosingFromViewModel;
    private bool _hasInitializedViewport;

    public PreviewEditorWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        Closing += OnClosing;
        ViewportControl.ZoomChanged += OnViewportZoomChanged;
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
        SetViewportMode(ImageViewportInteractionMode.Pan);
        SetTimelineMode(TimelineStripEditMode.Trim);
        RefreshFromViewModel();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PreviewEditorViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            oldViewModel.CloseRequested -= OnCloseRequested;
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

        TimelineStripControl.CutRequested -= OnCutRequested;
        TimelineStripControl.TrimChanged -= OnTrimChanged;
        ViewportControl.CropChanged -= OnCropChanged;

        TimelineStripControl.CutRequested += OnCutRequested;
        TimelineStripControl.TrimChanged += OnTrimChanged;
        ViewportControl.CropChanged += OnCropChanged;
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
                RefreshTimelineState();
                break;
            case nameof(PreviewEditorViewModel.PreviewImage):
            case nameof(PreviewEditorViewModel.PreviewPrimaryAxisLength):
                RefreshPreviewSurface();
                RefreshTimelineState();
                break;
            case nameof(PreviewEditorViewModel.PreviewSizeText):
                PreviewMetricsTextBlock.Text = ViewModel.PreviewSizeText;
                break;
            case nameof(PreviewEditorViewModel.ChromeSummary):
                ChromeSummaryTextBlock.Text = ViewModel.ChromeSummary;
                break;
            case nameof(PreviewEditorViewModel.EditSummary):
                EditSummaryTextBlock.Text = ViewModel.EditSummary;
                break;
            case nameof(PreviewEditorViewModel.SaveLocationHint):
                SaveLocationTextBlock.Text = ViewModel.SaveLocationHint;
                break;
            case nameof(PreviewEditorViewModel.HasUnsavedChanges):
                UpdateDirtyState();
                break;
            default:
                RefreshFromViewModel();
                break;
        }
    }

    private void RefreshFromViewModel()
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewportControl.SetCrop(ViewModel.CurrentState.CropRect);
        RefreshPreviewSurface();
        RefreshTimelineState();
        EditorTitleTextBlock.Text = ViewModel.HasTimeline ? "Finish your scrolling capture" : "Review your screenshot";
        EditorSubtitleTextBlock.Text = "Crop, trim, and remove anything you do not want before saving.";
        UpdateDirtyState();
        ChromeSummaryTextBlock.Text = ViewModel.ChromeSummary;
        EditSummaryTextBlock.Text = ViewModel.EditSummary;
        SaveLocationTextBlock.Text = ViewModel.SaveLocationHint;

        TimelineStripControl.Visibility = ViewModel.HasTimeline ? Visibility.Visible : Visibility.Collapsed;
        if (!ViewModel.HasTimeline)
        {
            RootGrid.ColumnDefinitions[1].Width = new GridLength(0);
            RootGrid.RowDefinitions[5].Height = new GridLength(0);
            return;
        }

        if (ViewModel.IsVerticalDirection)
        {
            RootGrid.ColumnDefinitions[1].Width = new GridLength(220);
            RootGrid.RowDefinitions[5].Height = new GridLength(0);
            Grid.SetRow(TimelineStripControl, 4);
            Grid.SetColumn(TimelineStripControl, 1);
            Grid.SetColumnSpan(TimelineStripControl, 1);
            TimelineStripControl.Margin = new Thickness(12, 0, 0, 0);
        }
        else
        {
            RootGrid.ColumnDefinitions[1].Width = new GridLength(0);
            RootGrid.RowDefinitions[5].Height = new GridLength(200);
            Grid.SetRow(TimelineStripControl, 5);
            Grid.SetColumn(TimelineStripControl, 0);
            Grid.SetColumnSpan(TimelineStripControl, 2);
            TimelineStripControl.Margin = new Thickness(0, 12, 0, 0);
        }
    }

    private void RefreshPreviewSurface()
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewportControl.SetImage(ViewModel.PreviewImage);
        PreviewMetricsTextBlock.Text = ViewModel.PreviewSizeText;

        if (!_hasInitializedViewport && ViewModel.PreviewImage is not null)
        {
            _hasInitializedViewport = true;
            ViewportControl.FitToView();
            UpdateZoomText(ViewportControl.ZoomFactor);
        }
    }

    private void RefreshTimelineState()
    {
        if (ViewModel is null)
        {
            return;
        }

        TimelineStripControl.SetState(
            ViewModel.PreviewImage,
            ViewModel.Direction,
            ViewModel.PreviewPrimaryAxisLength,
            ViewModel.CurrentState.TrimRange,
            ViewModel.CurrentState.CutRanges);
        TimelineStripControl.SetEditMode(_timelineMode);
    }

    private void UpdateDirtyState()
    {
        if (ViewModel is null)
        {
            return;
        }

        DirtyStateTextBlock.Text = ViewModel.HasUnsavedChanges ? "Unsaved changes" : "Saved";
        DirtyStateTextBlock.Foreground = (Brush)(TryFindResource(ViewModel.HasUnsavedChanges ? "DangerBrush" : "SuccessBrush") ?? Brushes.White);
    }

    private void OnCutRequested(object? sender, Models.CutRange e)
    {
        ViewModel?.AddCut(e);
    }

    private void OnTrimChanged(object? sender, Models.TrimRange e)
    {
        ViewModel?.ApplyTrim(e);
    }

    private void OnCropChanged(object? sender, Models.CropRect? e)
    {
        ViewModel?.SetCrop(e);
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

    private void OnPanModeClick(object sender, RoutedEventArgs e)
    {
        SetViewportMode(ImageViewportInteractionMode.Pan);
    }

    private void OnCropModeClick(object sender, RoutedEventArgs e)
    {
        SetViewportMode(ImageViewportInteractionMode.Crop);
    }

    private void OnTrimModeClick(object sender, RoutedEventArgs e)
    {
        SetTimelineMode(TimelineStripEditMode.Trim);
    }

    private void OnCutModeClick(object sender, RoutedEventArgs e)
    {
        SetTimelineMode(TimelineStripEditMode.Cut);
    }

    private void OnZoomOutClick(object sender, RoutedEventArgs e)
    {
        ViewportControl.ZoomOut();
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e)
    {
        ViewportControl.ZoomIn();
    }

    private void OnFitClick(object sender, RoutedEventArgs e)
    {
        ViewportControl.FitToView();
    }

    private void OnOneToOneClick(object sender, RoutedEventArgs e)
    {
        ViewportControl.SetOneToOne();
    }

    private void OnViewportZoomChanged(object? sender, double zoomFactor)
    {
        UpdateZoomText(zoomFactor);
    }

    private void SetViewportMode(ImageViewportInteractionMode mode)
    {
        _viewportMode = mode;
        ViewportControl.SetInteractionMode(mode);
        PanModeToggleButton.IsChecked = mode == ImageViewportInteractionMode.Pan;
        CropModeToggleButton.IsChecked = mode == ImageViewportInteractionMode.Crop;
        ViewportModeHintTextBlock.Text = mode == ImageViewportInteractionMode.Pan
            ? "Move lets you look around the image. Use the mouse wheel or the zoom buttons to get a closer look."
            : "Crop lets you drag a box around the part you want to keep. Use Clear Crop if you want to start over.";
    }

    private void SetTimelineMode(TimelineStripEditMode mode)
    {
        _timelineMode = mode;
        TimelineStripControl.SetEditMode(mode);
        TrimModeToggleButton.IsChecked = mode == TimelineStripEditMode.Trim;
        CutModeToggleButton.IsChecked = mode == TimelineStripEditMode.Cut;
        TimelineModeHintTextBlock.Text = mode == TimelineStripEditMode.Trim
            ? "Trim Ends shortens the capture from the beginning or end."
            : "Remove Part cuts out something in the middle. Drag across the strip where you want that section removed.";
    }

    private void UpdateZoomText(double zoomFactor)
    {
        ZoomTextBlock.Text = $"{zoomFactor:P0}";
    }
}
