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

public partial class SettingsWindow : Window
{
    private readonly ObservableCollection<SettingsUiModel.HotkeyOption<ModifierKeys>> _modifierOptions = new(SettingsUiModel.ModifierOptions);
    private readonly ObservableCollection<SettingsUiModel.HotkeyOption<Key>> _keyOptions = new(SettingsUiModel.KeyOptions);
    private readonly SettingsService _settingsService;
    private readonly Action<AppSettings> _applySettingsAction;
    private AppSettings _currentSettings;

    public SettingsWindow(
        SettingsService settingsService,
        Action<AppSettings> applySettingsAction,
        AppSettings currentSettings)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _applySettingsAction = applySettingsAction ?? throw new ArgumentNullException(nameof(applySettingsAction));
        _currentSettings = currentSettings;

        InitializeComponent();
        HotkeyModifiersComboBox.ItemsSource = _modifierOptions;
        HotkeyKeyComboBox.ItemsSource = _keyOptions;
        LoadSettings(currentSettings);
    }

    private void LoadSettings(AppSettings settings)
    {
        _currentSettings = settings;
        SaveFolderTextBox.Text = settings.SaveFolder;
        SelectOrInsertOption(_modifierOptions, HotkeyModifiersComboBox, settings.HotkeyModifiers, SettingsUiModel.FormatModifiers);
        SelectOrInsertOption(_keyOptions, HotkeyKeyComboBox, settings.HotkeyKey, SettingsUiModel.FormatKeyLabel);
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        ScrollCaptureDebugDumpEnabledCheckBox.IsChecked = settings.ScrollCaptureDebugDumpEnabled;
        DebugDumpFolderTextBox.Text = settings.DebugDumpFolder;
        UpdateHotkeySummary();
        StatusTextBlock.Text = "Ready.";
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
        if (HotkeyModifiersComboBox.SelectedItem is not SettingsUiModel.HotkeyOption<ModifierKeys> modifierOption ||
            HotkeyKeyComboBox.SelectedItem is not SettingsUiModel.HotkeyOption<Key> hotkeyOption)
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
        _applySettingsAction(settings);
        _currentSettings = settings;
        StatusTextBlock.Text = "Settings saved.";
        UpdateHotkeySummary();
    }

    private void UpdateHotkeySummary()
    {
        var modifierLabel = HotkeyModifiersComboBox.SelectedItem is SettingsUiModel.HotkeyOption<ModifierKeys> modifierOption
            ? modifierOption.Label
            : SettingsUiModel.FormatModifiers(_currentSettings.HotkeyModifiers);
        var keyLabel = HotkeyKeyComboBox.SelectedItem is SettingsUiModel.HotkeyOption<Key> keyOption
            ? keyOption.Label
            : SettingsUiModel.FormatKeyLabel(_currentSettings.HotkeyKey);
        HotkeyHelpTextBlock.Text = $"{modifierLabel} + {keyLabel}";
    }

    private static string? BrowseForFolder(string description, string initialPath)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = description,
            ShowNewFolderButton = true,
        };

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            dialog.InitialDirectory = initialPath;
        }

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

    private static void SelectOrInsertOption<T>(
        ObservableCollection<SettingsUiModel.HotkeyOption<T>> options,
        System.Windows.Controls.ComboBox comboBox,
        T value,
        Func<T, string> fallbackLabel)
        where T : struct
    {
        var matchingOption = options.FirstOrDefault(option => EqualityComparer<T>.Default.Equals(option.Value, value));
        if (matchingOption is null)
        {
            matchingOption = new SettingsUiModel.HotkeyOption<T>(fallbackLabel(value), value);
            options.Insert(0, matchingOption);
        }

        comboBox.SelectedItem = matchingOption;
    }
}
