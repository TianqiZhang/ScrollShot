using System.Windows;
using System.Windows.Input;
using ScrollShot.App.Models;
using ScrollShot.App.Services;

namespace ScrollShot.App;

public partial class MainWindow : Window
{
    private readonly CaptureOrchestrator? _captureOrchestrator;
    private readonly SettingsService? _settingsService;
    private readonly StartupRegistrationService? _startupRegistrationService;
    private readonly Action<AppSettings>? _applySettingsAction;
    private AppSettings _currentSettings = AppSettings.CreateDefault();

    public MainWindow()
    {
        InitializeComponent();
    }

    public bool AllowClose { get; set; }

    public MainWindow(
        CaptureOrchestrator captureOrchestrator,
        SettingsService settingsService,
        StartupRegistrationService startupRegistrationService,
        Action<AppSettings> applySettingsAction,
        AppSettings currentSettings)
        : this()
    {
        _captureOrchestrator = captureOrchestrator;
        _settingsService = settingsService;
        _startupRegistrationService = startupRegistrationService;
        _applySettingsAction = applySettingsAction;
        LoadSettings(currentSettings);
    }

    public void LoadSettings(AppSettings settings)
    {
        _currentSettings = settings;
        SaveFolderTextBox.Text = settings.SaveFolder;
        HotkeyModifiersTextBox.Text = settings.HotkeyModifiers.ToString();
        HotkeyKeyTextBox.Text = settings.HotkeyKey.ToString();
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        ScrollCaptureDebugDumpEnabledCheckBox.IsChecked = settings.ScrollCaptureDebugDumpEnabled;
        DebugDumpFolderTextBox.Text = settings.DebugDumpFolder;
        StatusTextBlock.Text = $"Hotkey: {settings.HotkeyModifiers}+{settings.HotkeyKey}";
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (AllowClose)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private async void OnNewCaptureClick(object sender, RoutedEventArgs e)
    {
        if (_captureOrchestrator is not null)
        {
            await _captureOrchestrator.BeginCaptureAsync();
        }
    }

    private void OnSaveSettingsClick(object sender, RoutedEventArgs e)
    {
        if (_settingsService is null || _applySettingsAction is null)
        {
            return;
        }

        if (!Enum.TryParse<ModifierKeys>(HotkeyModifiersTextBox.Text, ignoreCase: true, out var modifiers) ||
            !Enum.TryParse<Key>(HotkeyKeyTextBox.Text, ignoreCase: true, out var hotkeyKey))
        {
            StatusTextBlock.Text = "Invalid hotkey values. Example modifiers: Control, Shift";
            return;
        }

        var settings = new AppSettings
        {
            HotkeyModifiers = modifiers,
            HotkeyKey = hotkeyKey,
            SaveFolder = string.IsNullOrWhiteSpace(SaveFolderTextBox.Text) ? _currentSettings.SaveFolder : SaveFolderTextBox.Text.Trim(),
            StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false,
            ScrollCaptureDebugDumpEnabled = ScrollCaptureDebugDumpEnabledCheckBox.IsChecked ?? false,
            DebugDumpFolder = string.IsNullOrWhiteSpace(DebugDumpFolderTextBox.Text) ? _currentSettings.DebugDumpFolder : DebugDumpFolderTextBox.Text.Trim(),
        };

        _settingsService.Save(settings);
        _startupRegistrationService?.Apply(settings.StartWithWindows);
        _applySettingsAction(settings);
        StatusTextBlock.Text = "Settings saved.";
    }
}
