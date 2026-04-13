using System.Diagnostics;
using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using ScrollShot.App.Models;
using ScrollShot.App.Services;

namespace ScrollShot.App;

public partial class MainWindow : Window
{
    private readonly CaptureOrchestrator? _captureOrchestrator;
    private readonly SettingsService? _settingsService;
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
        Action<AppSettings> applySettingsAction,
        AppSettings currentSettings)
        : this()
    {
        _captureOrchestrator = captureOrchestrator;
        _settingsService = settingsService;
        _applySettingsAction = applySettingsAction;
        LoadSettings(currentSettings);
    }

    public void LoadSettings(AppSettings settings)
    {
        _currentSettings = settings;
        HotkeySummaryTextBlock.Text = SettingsUiModel.FormatHotkey(settings);
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

    private void OnOpenSaveFolderClick(object sender, RoutedEventArgs e)
    {
        OpenFolder(_currentSettings.SaveFolder);
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        if (_settingsService is null || _applySettingsAction is null)
        {
            return;
        }

        var window = new SettingsWindow(_settingsService, _applySettingsAction, _currentSettings)
        {
            Owner = this,
        };
        window.ShowDialog();
    }

    private static void OpenFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(
                exception.Message,
                "ScrollShot",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

}
