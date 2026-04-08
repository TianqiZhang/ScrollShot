using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ScrollShot.App.Interop;

namespace ScrollShot.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x5353;
    private HwndSource? _source;
    private IntPtr _windowHandle;

    public event EventHandler? HotkeyPressed;

    public bool Register(Window window, ModifierKeys modifiers, Key key)
    {
        ArgumentNullException.ThrowIfNull(window);

        Unregister();

        _windowHandle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WndProc);

        return HotkeyNativeMethods.RegisterHotKey(
            _windowHandle,
            HotkeyId,
            (uint)modifiers,
            (uint)KeyInterop.VirtualKeyFromKey(key));
    }

    public void Dispose()
    {
        Unregister();
    }

    private void Unregister()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            HotkeyNativeMethods.UnregisterHotKey(_windowHandle, HotkeyId);
            _windowHandle = IntPtr.Zero;
        }

        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotkeyNativeMethods.WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }
}
