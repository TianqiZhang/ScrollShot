using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ScrollShot.Editor.Commands;
using ScrollShot.Editor.Composition;
using ScrollShot.Editor.Helpers;
using ScrollShot.Editor.Infrastructure;
using ScrollShot.Editor.Models;
using ScrollShot.Editor.Services;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Editor.ViewModels;

public sealed class PreviewEditorViewModel : INotifyPropertyChanged
{
    private readonly CaptureResult _captureResult;
    private readonly IImageCompositor _imageCompositor;
    private readonly IClipboardService _clipboardService;
    private readonly IImageFileService _imageFileService;
    private readonly IConfirmationService _confirmationService;
    private readonly Func<DateTimeOffset> _nowProvider;
    private readonly RelayCommand _undoCommand;
    private readonly RelayCommand _redoCommand;
    private readonly RelayCommand _clearCropCommand;
    private BitmapSource? _previewImage;
    private string? _lastSavedPath;
    private EditState _currentState;
    private EditState? _lastSavedState;

    public PreviewEditorViewModel(
        CaptureResult captureResult,
        IImageCompositor? imageCompositor = null,
        IClipboardService? clipboardService = null,
        IImageFileService? imageFileService = null,
        IConfirmationService? confirmationService = null,
        string? saveFolder = null,
        Func<DateTimeOffset>? nowProvider = null)
    {
        _captureResult = captureResult ?? throw new ArgumentNullException(nameof(captureResult));
        _imageCompositor = imageCompositor ?? new ImageCompositor();
        _clipboardService = clipboardService ?? new ClipboardService();
        _imageFileService = imageFileService ?? new ImageFileService();
        _confirmationService = confirmationService ?? new ConfirmationService();
        _nowProvider = nowProvider ?? (() => DateTimeOffset.Now);
        SaveFolder = saveFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
        EditCommands = new EditCommandStack(EditState.Default);
        _currentState = EditCommands.CurrentState;

        _undoCommand = new RelayCommand(() => ApplyState(EditCommands.Undo()), () => EditCommands.CanUndo);
        _redoCommand = new RelayCommand(() => ApplyState(EditCommands.Redo()), () => EditCommands.CanRedo);
        _clearCropCommand = new RelayCommand(() => SetCrop(null), () => CurrentState.CropRect is not null);
        SaveCommand = new RelayCommand(Save);
        CopyCommand = new RelayCommand(Copy);
        DiscardCommand = new RelayCommand(Discard);
        ToggleChromeCommand = new RelayCommand(() => ApplyState(EditCommands.Apply(new ChromeToggleCommand(!CurrentState.IncludeChrome))));

        RefreshPreview();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<PreviewEditorCloseRequestedEventArgs>? CloseRequested;

    public EditCommandStack EditCommands { get; }

    public ScrollDirection Direction => _captureResult.Direction;

    public bool IsVerticalDirection => Direction == ScrollDirection.Vertical;

    public bool HasTimeline => _captureResult.IsScrollingCapture;

    public bool IsScrollingCapture => _captureResult.IsScrollingCapture;

    public EditState CurrentState
    {
        get => _currentState;
        private set
        {
            _currentState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(HasCrop));
            OnPropertyChanged(nameof(EditSummary));
            OnPropertyChanged(nameof(ChromeSummary));
        }
    }

    public bool HasUnsavedChanges => _lastSavedState is null
        ? EditCommands.CanUndo
        : !CurrentState.Equals(_lastSavedState);

    public string SaveFolder { get; }

    public string SaveLocationHint => LastSavedPath is null
        ? $"Images are saved to {SaveFolder}"
        : $"Last saved to {LastSavedPath}";

    public string? LastSavedPath
    {
        get => _lastSavedPath;
        private set
        {
            _lastSavedPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SaveLocationHint));
        }
    }

    public BitmapSource? PreviewImage
    {
        get => _previewImage;
        private set
        {
            _previewImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewPrimaryAxisLength));
            OnPropertyChanged(nameof(PreviewSizeText));
        }
    }

    public int PreviewPrimaryAxisLength => PreviewImage is null
        ? 0
        : IsVerticalDirection
            ? PreviewImage.PixelHeight
            : PreviewImage.PixelWidth;

    public string PreviewSizeText => PreviewImage is null
        ? "No preview"
        : $"{PreviewImage.PixelWidth} × {PreviewImage.PixelHeight} px";

    public bool HasCrop => CurrentState.CropRect is not null;

    public string ChromeSummary => CurrentState.IncludeChrome
        ? "The window frame will stay in the saved image."
        : "The window frame will be removed from the saved image.";

    public string EditSummary => BuildEditSummary(CurrentState);

    public ICommand UndoCommand => _undoCommand;

    public ICommand RedoCommand => _redoCommand;

    public ICommand ClearCropCommand => _clearCropCommand;

    public ICommand SaveCommand { get; }

    public ICommand CopyCommand { get; }

    public ICommand DiscardCommand { get; }

    public ICommand ToggleChromeCommand { get; }

    public void ApplyTrim(TrimRange trimRange)
    {
        ApplyState(EditCommands.Apply(new TrimCommand(trimRange)));
    }

    public void AddCut(CutRange cutRange)
    {
        ApplyState(EditCommands.Apply(new CutCommand(cutRange)));
    }

    public void SetCrop(CropRect? cropRect)
    {
        ApplyState(EditCommands.Apply(new CropCommand(cropRect)));
    }

    private void Save()
    {
        using var bitmap = ComposeBitmap();
        var timestamp = _nowProvider().ToString("yyyyMMdd_HHmmss");
        var path = Path.Combine(SaveFolder, $"ScrollShot_{timestamp}.png");
        _imageFileService.SavePng(bitmap, path);
        LastSavedPath = path;
        _lastSavedState = CurrentState;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void Copy()
    {
        using var bitmap = ComposeBitmap();
        _clipboardService.SetImage(bitmap);
    }

    private void Discard()
    {
        if (HasUnsavedChanges && !_confirmationService.ConfirmDiscard())
        {
            return;
        }

        CloseRequested?.Invoke(this, new PreviewEditorCloseRequestedEventArgs(true));
    }

    private void ApplyState(EditState state)
    {
        CurrentState = state;
        RefreshPreview();
        _undoCommand.RaiseCanExecuteChanged();
        _redoCommand.RaiseCanExecuteChanged();
        _clearCropCommand.RaiseCanExecuteChanged();
    }

    private void RefreshPreview()
    {
        using var bitmap = ComposeBitmap();
        PreviewImage = BitmapSourceConversion.ToBitmapSource(bitmap);
    }

    private Bitmap ComposeBitmap()
    {
        return _imageCompositor.Compose(_captureResult, CurrentState);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string BuildEditSummary(EditState state)
    {
        var parts = new List<string>();
        if (state.TrimRange.HeadTrimPixels > 0 || state.TrimRange.TailTrimPixels > 0)
        {
            parts.Add($"Trim {state.TrimRange.HeadTrimPixels}px / {state.TrimRange.TailTrimPixels}px");
        }

        if (state.CutRanges.Count > 0)
        {
            parts.Add($"{state.CutRanges.Count} cut{(state.CutRanges.Count == 1 ? string.Empty : "s")}");
        }

        if (state.CropRect is { } cropRect)
        {
            parts.Add($"Crop {cropRect.Width} × {cropRect.Height}");
        }

        if (!state.IncludeChrome)
        {
            parts.Add("Window frame removed");
        }

        return parts.Count == 0
            ? ""
            : string.Join(" \u00b7 ", parts);
    }
}
