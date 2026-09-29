using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace SnipCanvas;

internal sealed partial class MainWindow
{
    private Forms.NotifyIcon? tray;
    private System.Drawing.Icon? trayIcon;
    private Forms.ContextMenuStrip? trayMenu;
    private bool exiting;
    internal void AllowSessionExit() => exiting = true;
    internal bool TrayVisible => tray?.Visible == true;

    private void InitializeTray()
    {
        trayIcon = LoadTrayIcon();
        trayMenu = BuildTrayMenu();
        ApplyTrayTheme();
        tray = new Forms.NotifyIcon { Text = "SnipCanvas · " + preferences.Shortcut.Label, Icon = trayIcon, ContextMenuStrip = trayMenu, Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) RestoreFromTray(); };
    }

    internal void RestoreFromTray()
    {
        if (busy) return;
        DismissCapturePreview();
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private void HandleClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (exiting || tray == null) return;
        e.Cancel = true;
        CancelDrag(); Hide();
    }

    internal void Quit()
    {
        if (busy || !IsEnabled) return;
        CancelDrag();
        if (dirty && bitmap != null)
        {
            try
            {
                SavePreviousScreenshot(bitmap, preferences.SaveFolder);
                dirty = false;
            }
            catch (Exception ex)
            {
                ShowEditor(); RestoreFromTray();
                Dialogs.Problem(this, "SnipCanvas is still open", "Your screenshot couldn't be saved, so it was kept open to protect your edits. Choose another screenshot folder in Settings, then quit again.", ex.Message);
                return;
            }
        }
        exiting = true; Close();
    }

    private void DisposeTray()
    {
        if (tray != null) { tray.Visible = false; tray.Dispose(); }
        trayMenu?.Dispose(); trayIcon?.Dispose();
    }

    private void SetStartup(bool enabled)
    {
        try
        {
            StartupSetting.SetEnabled(enabled);
            startupEnabled = enabled;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException || ex is InvalidOperationException)
        {
            Dialogs.Problem(this, "The startup setting couldn't be changed", "Windows didn't allow SnipCanvas to change it.", ex.Message);
        }
    }
}

internal static class StartupSetting
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SnipCanvas";
    internal static string Command => "\"" + Environment.ProcessPath + "\" --tray";
    internal static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
    }
    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
        else key.DeleteValue(ValueName, false);
    }
}
