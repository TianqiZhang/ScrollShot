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

        ViewportControl.CropChanged -= OnCropChanged;
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
                break;
            case nameof(PreviewEditorViewModel.PreviewImage):
            case nameof(PreviewEditorViewModel.PreviewPrimaryAxisLength):
                RefreshPreviewSurface();
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
        EditorTitleTextBlock.Text = ViewModel.HasTimeline ? "Finish your scrolling capture" : "Review your screenshot";
        EditorSubtitleTextBlock.Text = "Crop, trim, and remove anything you do not want before saving.";
        UpdateDirtyState();
        ChromeSummaryTextBlock.Text = ViewModel.ChromeSummary;
        EditSummaryTextBlock.Text = ViewModel.EditSummary;
        SaveLocationTextBlock.Text = ViewModel.SaveLocationHint;
    }

    private void RefreshPreviewSurface()
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewportControl.SetImage(ViewModel.PreviewImage);
        PreviewMetricsTextBlock.Text = ViewModel.PreviewSizeText;

        if (!_hasInitializedViewport &&
            ViewModel.PreviewImage is not null &&
            ViewportControl.FitToView())
        {
            _hasInitializedViewport = true;
            UpdateZoomText(ViewportControl.ZoomFactor);
        }
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

    private void UpdateZoomText(double zoomFactor)
    {
        ZoomTextBlock.Text = $"{zoomFactor:P0}";
    }
}
