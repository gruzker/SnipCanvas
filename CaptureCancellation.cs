using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace SnipCanvas;

// Exists only during capture, including the delay before the overlay appears.
internal sealed class CaptureCancellation : IDisposable
{
    private delegate IntPtr KeyboardProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int id, KeyboardProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);

    private readonly CancellationTokenSource cancellation = new();
    private readonly KeyboardProc callback;
    private IntPtr hook;
    public CancellationToken Token => cancellation.Token;

    public CaptureCancellation()
    {
        callback = OnKeyboard;
        hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            cancellation.Dispose();
            throw new Win32Exception(error, "Could not enable Escape to cancel capture.");
        }
    }

    private IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && (message.ToInt32() == 0x100 || message.ToInt32() == 0x104)
            && Marshal.ReadInt32(data) == 0x1B)
        {
            // Token callbacks only queue UI work; never close a modal window inside the hook.
            cancellation.Cancel();
            return new IntPtr(1);
        }
        return CallNextHookEx(hook, code, message, data);
    }

    public void Dispose()
    {
        if (hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(hook);
        hook = IntPtr.Zero;
        cancellation.Dispose();
        GC.KeepAlive(callback);
    }
}
