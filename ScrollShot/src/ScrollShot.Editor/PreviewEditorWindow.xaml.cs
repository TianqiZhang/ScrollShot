using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
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
                UpdateCropAffordances();
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
        CutBandToggleButton.Visibility = ViewModel.IsScrollingCapture ? Visibility.Visible : Visibility.Collapsed;
        UpdateCropAffordances();
    }

    private void RefreshPreviewSurface()
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewportControl.SetImage(ViewModel.PreviewImage);
        ViewportControl.SetCrop(ViewModel.CurrentState.CropRect);
        ViewportControl.SetCutBands(ViewModel.CurrentState.CutRanges, ViewModel.Direction);
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

    private void UpdateCropAffordances()
    {
        if (ViewModel is null)
        {
            return;
        }

        CropHintTextBlock.Text = ViewModel.HasCrop
            ? "Drag the crop box or its handles to adjust it. Press Esc or use Clear Crop to remove it."
            : "Drag on the image to crop.";
        ClearCropButton.Visibility = ViewModel.HasCrop ? Visibility.Visible : Visibility.Collapsed;
        EditSummaryTextBlock.Visibility = string.IsNullOrWhiteSpace(ViewModel.EditSummary) ? Visibility.Collapsed : Visibility.Visible;
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
