using System.Windows.Input;
using System.IO;

namespace ScrollShot.App.Models;

public sealed class AppSettings
{
    public ModifierKeys HotkeyModifiers { get; init; }

    public Key HotkeyKey { get; init; }

    public string SaveFolder { get; init; } = string.Empty;

    public bool StartWithWindows { get; init; }

    public bool ScrollCaptureDebugDumpEnabled { get; init; }

    public string DebugDumpFolder { get; init; } = string.Empty;

    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            HotkeyModifiers = ModifierKeys.Control | ModifierKeys.Shift,
            HotkeyKey = Key.S,
            SaveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots"),
            StartWithWindows = false,
            ScrollCaptureDebugDumpEnabled = false,
            DebugDumpFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ScrollShot",
                "DebugDumps"),
        };
    }
}
