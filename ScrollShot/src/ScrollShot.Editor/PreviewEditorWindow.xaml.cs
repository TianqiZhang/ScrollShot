using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ScrollShot.Editor.ViewModels;

namespace ScrollShot.Editor;

public partial class PreviewEditorWindow : Window
{
    public PreviewEditorWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
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
        RefreshFromViewModel();
    }

    private void RefreshFromViewModel()
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewportControl.SetImage(ViewModel.PreviewImage);
        ViewportControl.SetCrop(ViewModel.CurrentState.CropRect);
        TimelineStripControl.SetState(
            ViewModel.PreviewImage,
            ViewModel.Direction,
            ViewModel.PreviewPrimaryAxisLength,
            ViewModel.CurrentState.TrimRange,
            ViewModel.CurrentState.CutRanges);

        if (ViewModel.IsVerticalDirection)
        {
            RootGrid.ColumnDefinitions[1].Width = new GridLength(220);
            RootGrid.RowDefinitions[2].Height = new GridLength(0);
            Grid.SetRow(TimelineStripControl, 1);
            Grid.SetColumn(TimelineStripControl, 1);
            Grid.SetColumnSpan(TimelineStripControl, 1);
        }
        else
        {
            RootGrid.ColumnDefinitions[1].Width = new GridLength(0);
            RootGrid.RowDefinitions[2].Height = GridLength.Auto;
            Grid.SetRow(TimelineStripControl, 2);
            Grid.SetColumn(TimelineStripControl, 0);
            Grid.SetColumnSpan(TimelineStripControl, 2);
            TimelineStripControl.Margin = new Thickness(8, 0, 8, 8);
        }
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
            Close();
        }
    }
}
