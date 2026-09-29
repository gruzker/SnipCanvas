using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Input;

namespace SnipCanvas;

internal readonly record struct CaptureHotkey(uint Key, uint Modifiers)
{
    public static CaptureHotkey Default => new(0x2C, 0);
    public bool IsValid => (Modifiers & ~15u) == 0 && Key >= 0x08 && Key <= 0xFE
        && Key is not (0x10 or 0x11 or 0x12 or 0x1B or 0x5B or 0x5C or 0x7B or >= 0xA0 and <= 0xA5)
        && (Key == 0x2C || Key is >= 0x70 and <= 0x87 || (Modifiers & 11) != 0);
    public string Label => ((Modifiers & 2) != 0 ? "Ctrl + " : "")
        + ((Modifiers & 1) != 0 ? "Alt + " : "")
        + ((Modifiers & 4) != 0 ? "Shift + " : "")
        + ((Modifiers & 8) != 0 ? "Win + " : "")
        + (Key == 0x2C ? "Print Screen" : new KeyConverter().ConvertToString(KeyInterop.KeyFromVirtualKey((int)Key)));
}

internal sealed class GlobalCaptureShortcut : IDisposable
{
    private int id = 0x5354;
    private readonly HwndSource source;
    private readonly Action capture;
    public bool Registered { get; private set; }
    public CaptureHotkey Gesture { get; private set; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    public GlobalCaptureShortcut(HwndSource source, Action capture, CaptureHotkey? gesture = null)
    {
        this.source = source; this.capture = capture;
        Gesture = gesture is { IsValid: true } value ? value : CaptureHotkey.Default;
        source.AddHook(OnMessage);
        TryRegister();
    }

    public bool TryRegister() => Registered || (Registered = RegisterHotKey(source.Handle, id, 0x4000 | Gesture.Modifiers, Gesture.Key));

    public bool TryChange(CaptureHotkey gesture)
    {
        if (!gesture.IsValid) return false;
        if (gesture == Gesture) return TryRegister();
        int nextId = id == 0x5354 ? 0x5355 : 0x5354;
        if (!RegisterHotKey(source.Handle, nextId, 0x4000 | gesture.Modifiers, gesture.Key)) return false;
        Suspend();
        id = nextId; Gesture = gesture; Registered = true;
        return true;
    }

    public void Suspend()
    {
        if (Registered) UnregisterHotKey(source.Handle, id);
        Registered = false;
    }

    private IntPtr OnMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam.ToInt32() == id && Registered)
        {
            handled = true;
            // Leave the native message handler before opening capture UI.
            source.Dispatcher.BeginInvoke(capture);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Suspend();
        source.RemoveHook(OnMessage);
    }
}
