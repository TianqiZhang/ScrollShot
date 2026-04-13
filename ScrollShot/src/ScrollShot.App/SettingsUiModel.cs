using System.Collections.ObjectModel;
using System.Windows.Input;
using ScrollShot.App.Models;

namespace ScrollShot.App;

internal static class SettingsUiModel
{
    public static ReadOnlyCollection<HotkeyOption<ModifierKeys>> ModifierOptions { get; } = BuildModifierOptions();

    public static ReadOnlyCollection<HotkeyOption<Key>> KeyOptions { get; } = BuildKeyOptions();

    public static string FormatHotkey(AppSettings settings)
    {
        return $"{FormatModifiers(settings.HotkeyModifiers)} + {FormatKeyLabel(settings.HotkeyKey)}";
    }

    public static string FormatModifiers(ModifierKeys modifiers)
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

    public static string FormatKeyLabel(Key key)
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

    internal sealed record HotkeyOption<T>(string Label, T Value);
}
