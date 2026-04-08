using FluentAssertions;
using ScrollShot.App.Models;
using ScrollShot.App.Services;
using System.IO;
using System.Windows.Input;

namespace ScrollShot.App.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _settingsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.settings.json");

    [Fact]
    public void Load_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var service = new SettingsService(_settingsPath);

        var settings = service.Load();

        settings.HotkeyModifiers.Should().Be(ModifierKeys.Control | ModifierKeys.Shift);
        settings.HotkeyKey.Should().Be(Key.S);
        settings.StartWithWindows.Should().BeFalse();
        settings.ScrollCaptureDebugDumpEnabled.Should().BeFalse();
        settings.DebugDumpFolder.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SaveAndLoad_RoundTripsSettings()
    {
        var service = new SettingsService(_settingsPath);
        var expected = new AppSettings
        {
            HotkeyModifiers = ModifierKeys.Alt,
            HotkeyKey = Key.A,
            SaveFolder = @"C:\captures",
            StartWithWindows = true,
            ScrollCaptureDebugDumpEnabled = true,
            DebugDumpFolder = @"C:\captures\debug-dumps",
        };

        service.Save(expected);
        var actual = service.Load();

        actual.HotkeyModifiers.Should().Be(expected.HotkeyModifiers);
        actual.HotkeyKey.Should().Be(expected.HotkeyKey);
        actual.SaveFolder.Should().Be(expected.SaveFolder);
        actual.StartWithWindows.Should().BeTrue();
        actual.ScrollCaptureDebugDumpEnabled.Should().BeTrue();
        actual.DebugDumpFolder.Should().Be(expected.DebugDumpFolder);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileIsCorrupted()
    {
        File.WriteAllText(_settingsPath, "NOT VALID JSON {{{");
        var service = new SettingsService(_settingsPath);

        var settings = service.Load();

        settings.HotkeyModifiers.Should().Be(ModifierKeys.Control | ModifierKeys.Shift);
        settings.HotkeyKey.Should().Be(Key.S);
    }

    public void Dispose()
    {
        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }
    }
}
