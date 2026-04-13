using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using ScrollShot.App.Models;
using ScrollShot.App.Services;

namespace ScrollShot.App;

public partial class MainWindow : Window
{
    private static readonly ReadOnlyCollection<HotkeyOption<ModifierKeys>> ModifierOptions = BuildModifierOptions();
    private static readonly ReadOnlyCollection<HotkeyOption<Key>> KeyOptions = BuildKeyOptions();
    private readonly ObservableCollection<HotkeyOption<ModifierKeys>> _modifierOptions = new(ModifierOptions);
    private readonly ObservableCollection<HotkeyOption<Key>> _keyOptions = new(KeyOptions);
    private readonly CaptureOrchestrator? _captureOrchestrator;
    private readonly SettingsService? _settingsService;
    private readonly StartupRegistrationService? _startupRegistrationService;
    private readonly Action<AppSettings>? _applySettingsAction;
    private AppSettings _currentSettings = AppSettings.CreateDefault();

    public MainWindow()
    {
        InitializeComponent();
        HotkeyModifiersComboBox.ItemsSource = _modifierOptions;
        HotkeyKeyComboBox.ItemsSource = _keyOptions;
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
        SelectOrInsertOption(_modifierOptions, HotkeyModifiersComboBox, settings.HotkeyModifiers, static value => FormatModifiers(value));
        SelectOrInsertOption(_keyOptions, HotkeyKeyComboBox, settings.HotkeyKey, FormatKeyLabel);
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        ScrollCaptureDebugDumpEnabledCheckBox.IsChecked = settings.ScrollCaptureDebugDumpEnabled;
        DebugDumpFolderTextBox.Text = settings.DebugDumpFolder;
        UpdateHotkeySummary();
        StatusTextBlock.Text = "Ready to capture.";
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
        OpenFolder(SaveFolderTextBox.Text);
    }

    private void OnBrowseSaveFolderClick(object sender, RoutedEventArgs e)
    {
        if (BrowseForFolder("Choose where ScrollShot saves finished images.", SaveFolderTextBox.Text) is { } folder)
        {
            SaveFolderTextBox.Text = folder;
        }
    }

    private void OnBrowseDebugDumpFolderClick(object sender, RoutedEventArgs e)
    {
        if (BrowseForFolder("Choose where ScrollShot stores debug dump sessions.", DebugDumpFolderTextBox.Text) is { } folder)
        {
            DebugDumpFolderTextBox.Text = folder;
        }
    }

    private void OnHotkeySelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateHotkeySummary();
    }

    private void OnSaveSettingsClick(object sender, RoutedEventArgs e)
    {
        if (_settingsService is null || _applySettingsAction is null)
        {
            return;
        }

        if (HotkeyModifiersComboBox.SelectedItem is not HotkeyOption<ModifierKeys> modifierOption ||
            HotkeyKeyComboBox.SelectedItem is not HotkeyOption<Key> hotkeyOption)
        {
            StatusTextBlock.Text = "Choose both a modifier combination and a hotkey key before saving.";
            return;
        }

        var settings = new AppSettings
        {
            HotkeyModifiers = modifierOption.Value,
            HotkeyKey = hotkeyOption.Value,
            SaveFolder = string.IsNullOrWhiteSpace(SaveFolderTextBox.Text) ? _currentSettings.SaveFolder : SaveFolderTextBox.Text.Trim(),
            StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false,
            ScrollCaptureDebugDumpEnabled = ScrollCaptureDebugDumpEnabledCheckBox.IsChecked ?? false,
            DebugDumpFolder = string.IsNullOrWhiteSpace(DebugDumpFolderTextBox.Text) ? _currentSettings.DebugDumpFolder : DebugDumpFolderTextBox.Text.Trim(),
        };

        _settingsService.Save(settings);
        _startupRegistrationService?.Apply(settings.StartWithWindows);
        _applySettingsAction(settings);
        StatusTextBlock.Text = "Settings saved.";
        UpdateHotkeySummary();
    }

    private void UpdateHotkeySummary()
    {
        var modifierLabel = HotkeyModifiersComboBox.SelectedItem is HotkeyOption<ModifierKeys> modifierOption
            ? modifierOption.Label
            : FormatModifiers(_currentSettings.HotkeyModifiers);
        var keyLabel = HotkeyKeyComboBox.SelectedItem is HotkeyOption<Key> keyOption
            ? keyOption.Label
            : FormatKeyLabel(_currentSettings.HotkeyKey);
        var summary = $"{modifierLabel} + {keyLabel}";
        HotkeySummaryTextBlock.Text = summary;
        HotkeyHelpTextBlock.Text = summary;
    }

    private static string? BrowseForFolder(string description, string initialPath)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = description,
            ShowNewFolderButton = true,
            InitialDirectory = string.IsNullOrWhiteSpace(initialPath) ? null : initialPath,
        };

        return dialog.ShowDialog() == Forms.DialogResult.OK
            ? dialog.SelectedPath
            : null;
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

    private static ReadOnlyCollection<HotkeyOption<ModifierKeys>> BuildModifierOptions()
    {
        return new[]
        {
            new HotkeyOption<ModifierKeys>("Ctrl", ModifierKeys.Control),
            new HotkeyOption<ModifierKeys>("Alt", ModifierKeys.Alt),
            new HotkeyOption<ModifierKeys>("Shift", ModifierKeys.Shift),
            new HotkeyOption<ModifierKeys>("Ctrl + Shift", ModifierKeys.Control | ModifierKeys.Shift),
            new HotkeyOption<ModifierKeys>("Ctrl + Alt", ModifierKeys.Control | ModifierKeys.Alt),
            new HotkeyOption<ModifierKeys>("Alt + Shift", ModifierKeys.Alt | ModifierKeys.Shift),
            new HotkeyOption<ModifierKeys>("Ctrl + Alt + Shift", ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift),
        }.ToList().AsReadOnly();
    }

    private static ReadOnlyCollection<HotkeyOption<Key>> BuildKeyOptions()
    {
        var options = new List<HotkeyOption<Key>>();
        for (var digit = 0; digit <= 9; digit++)
        {
            options.Add(new HotkeyOption<Key>(digit.ToString(), Key.D0 + digit));
        }

        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            options.Add(new HotkeyOption<Key>(letter.ToString(), Key.A + (letter - 'A')));
        }

        for (var functionIndex = 1; functionIndex <= 12; functionIndex++)
        {
            options.Add(new HotkeyOption<Key>($"F{functionIndex}", Key.F1 + (functionIndex - 1)));
        }

        options.Add(new HotkeyOption<Key>("Print Screen", Key.PrintScreen));
        return options.AsReadOnly();
    }

    private static void SelectOrInsertOption<T>(
        ObservableCollection<HotkeyOption<T>> options,
        System.Windows.Controls.ComboBox comboBox,
        T value,
        Func<T, string> fallbackLabel)
        where T : struct
    {
        var matchingOption = options.FirstOrDefault(option => EqualityComparer<T>.Default.Equals(option.Value, value));
        if (matchingOption is null)
        {
            matchingOption = new HotkeyOption<T>(fallbackLabel(value), value);
            options.Insert(0, matchingOption);
        }

        comboBox.SelectedItem = matchingOption;
    }

    private static string FormatModifiers(ModifierKeys modifiers)
    {
        if (modifiers == ModifierKeys.None)
        {
            return "None";
        }

        var labels = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            labels.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            labels.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            labels.Add("Shift");
        }

        return string.Join(" + ", labels);
    }

    private static string FormatKeyLabel(Key key)
    {
        if (key >= Key.D0 && key <= Key.D9)
        {
            return ((int)(key - Key.D0)).ToString();
        }

        return key switch
        {
            Key.PrintScreen => "Print Screen",
            _ => key.ToString(),
        };
    }

    private sealed record HotkeyOption<T>(string Label, T Value);
}
