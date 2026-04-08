using System.Windows;
using ScrollShot.App.Models;
using ScrollShot.App.Services;

namespace ScrollShot.App;

public partial class App : Application
{
    private TrayIconManager? _trayIconManager;
    private GlobalHotkeyService? _hotkeyService;
    private SettingsService? _settingsService;
    private StartupRegistrationService? _startupRegistrationService;
    private CaptureOrchestrator? _captureOrchestrator;
    private MainWindow? _mainWindow;
    private AppSettings _currentSettings = AppSettings.CreateDefault();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}",
                "ScrollShot Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        _settingsService = new SettingsService();
        _startupRegistrationService = new StartupRegistrationService();
        _currentSettings = _settingsService.Load();

        _captureOrchestrator = new CaptureOrchestrator(() => _currentSettings);
        _trayIconManager = new TrayIconManager();
        _hotkeyService = new GlobalHotkeyService();
        _mainWindow = new MainWindow(_captureOrchestrator, _settingsService, _startupRegistrationService, ApplySettings, _currentSettings);

        _trayIconManager.NewCaptureRequested += async (_, _) => await _captureOrchestrator.BeginCaptureAsync();
        _trayIconManager.ShowWindowRequested += (_, _) => ShowMainWindow();
        _trayIconManager.ExitRequested += (_, _) =>
        {
            if (_mainWindow is not null)
            {
                _mainWindow.AllowClose = true;
            }

            Shutdown();
        };
        _hotkeyService.HotkeyPressed += async (_, _) => await _captureOrchestrator.BeginCaptureAsync();

        _mainWindow.Show();
        _trayIconManager.Initialize();
        ApplySettings(_currentSettings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayIconManager?.Dispose();
        base.OnExit(e);
    }

    private void ApplySettings(AppSettings settings)
    {
        _currentSettings = settings;
        _startupRegistrationService?.Apply(settings.StartWithWindows);

        if (_mainWindow is not null && _hotkeyService is not null)
        {
            var registered = _hotkeyService.Register(_mainWindow, settings.HotkeyModifiers, settings.HotkeyKey);
            if (!registered)
            {
                MessageBox.Show(
                    $"The hotkey {settings.HotkeyModifiers}+{settings.HotkeyKey} could not be registered.",
                    "ScrollShot",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        _mainWindow?.LoadSettings(_currentSettings);
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }
}

