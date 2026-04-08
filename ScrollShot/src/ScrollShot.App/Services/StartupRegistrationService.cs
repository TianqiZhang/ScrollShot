using Microsoft.Win32;

namespace ScrollShot.App.Services;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScrollShot";

    public void Apply(bool enabled)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (runKey is null)
        {
            return;
        }

        if (enabled)
        {
            runKey.SetValue(ValueName, Environment.ProcessPath ?? string.Empty);
        }
        else
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
