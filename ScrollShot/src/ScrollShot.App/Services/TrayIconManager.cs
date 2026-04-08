using System.Drawing;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;

namespace ScrollShot.App.Services;

public sealed class TrayIconManager : IDisposable
{
    private TaskbarIcon? _taskbarIcon;

    public event EventHandler? NewCaptureRequested;

    public event EventHandler? ShowWindowRequested;

    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_taskbarIcon is not null)
        {
            return;
        }

        _taskbarIcon = new TaskbarIcon
        {
            Icon = SystemIcons.Application,
            ToolTipText = "ScrollShot",
        };

        _taskbarIcon.TrayMouseDoubleClick += (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        _taskbarIcon.ContextMenu = new ContextMenu
        {
            Items =
            {
                CreateMenuItem("New Capture", () => NewCaptureRequested?.Invoke(this, EventArgs.Empty)),
                CreateMenuItem("Show", () => ShowWindowRequested?.Invoke(this, EventArgs.Empty)),
                CreateMenuItem("Exit", () => ExitRequested?.Invoke(this, EventArgs.Empty)),
            },
        };
    }

    public void Dispose()
    {
        _taskbarIcon?.Dispose();
        _taskbarIcon = null;
    }

    private static MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }
}
