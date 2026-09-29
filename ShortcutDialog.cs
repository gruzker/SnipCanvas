using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SnipCanvas;

internal sealed partial class MainWindow
{
    private void ChangeCaptureShortcut()
    {
        if (globalShortcut == null) return;
        var dialog = Dialogs.Create(this, "Capture shortcut", 480);
        NameScope.SetNameScope(dialog, new NameScope());
        var panel = new StackPanel { Margin = new Thickness(26, 24, 26, 22) };
        panel.Children.Add(Ui.Text("Change capture shortcut", 18, "Ink", FontWeights.SemiBold));
        var help = Ui.Text("Press the keys you want to use. Combine Ctrl, Alt or Win with a key, or choose Print Screen or a function key. F12 is reserved by Windows.", 13, "Muted", wrap: true);
        help.Margin = new Thickness(0, 8, 0, 18); panel.Children.Add(help);

        var candidate = preferences.Shortcut;
        var keys = new Border { MinHeight = 92, CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Focusable = true, Cursor = Cursors.Arrow, FocusVisualStyle = null };
        keys.SetResourceReference(Border.BackgroundProperty, "Field");
        keys.SetResourceReference(Border.BorderBrushProperty, "Line");
        keys.GotKeyboardFocus += (_, _) => keys.SetResourceReference(Border.BorderBrushProperty, "Accent");
        keys.LostKeyboardFocus += (_, _) => keys.SetResourceReference(Border.BorderBrushProperty, "Line");
        var display = new TextBlock { Text = candidate.Label, Visibility = Visibility.Collapsed };
        void ShowCandidate()
        {
            var caps = Ui.Kbd(candidate.Label, 17); caps.HorizontalAlignment = HorizontalAlignment.Center;
            keys.Child = caps; display.Text = candidate.Label;
        }
        ShowCandidate();
        System.Windows.Automation.AutomationProperties.SetName(keys, "Press your new shortcut");
        dialog.RegisterName("KeyInput", keys); dialog.RegisterName("KeyLabel", display);
        panel.Children.Add(keys);

        var error = Ui.Text("", 12.5, "Warning", wrap: true); error.Margin = new Thickness(2, 10, 0, 0); error.MinHeight = 18;
        panel.Children.Add(error);

        var actions = new DockPanel { Margin = new Thickness(0, 16, 0, 0), LastChildFill = false };
        Button? save = null;
        var reset = Ui.Styled("LinkButton", "Use Print Screen", () => {
            candidate = CaptureHotkey.Default; ShowCandidate(); error.Text = ""; save!.IsEnabled = true; keys.Focus();
        }, "Go back to the default shortcut");
        reset.Margin = new Thickness(-8, 0, 0, 0);
        DockPanel.SetDock(reset, Dock.Left); actions.Children.Add(reset);
        save = Ui.Styled("PrimaryButton", "Save", () => {
            if (!globalShortcut.TryChange(candidate)) { error.Text = "That shortcut is unavailable. Try another combination or close the app using it."; keys.Focus(); return; }
            var previous = preferences.Shortcut; preferences.Shortcut = candidate;
            if (!SavePreferences()) {
                preferences.Shortcut = previous;
                globalShortcut.TryChange(previous);
                error.Text = "The shortcut wasn't saved. Try again.";
                globalShortcut.Suspend(); keys.Focus(); return;
            }
            dialog.DialogResult = true;
        });
        save.MinWidth = 88; save.Margin = new Thickness(8, 0, 0, 0);
        var cancel = Ui.Styled("Button", "Cancel", () => dialog.DialogResult = false); cancel.IsCancel = true; cancel.MinWidth = 88;
        DockPanel.SetDock(save, Dock.Right); actions.Children.Add(save);
        DockPanel.SetDock(cancel, Dock.Right); actions.Children.Add(cancel);
        dialog.RegisterName("ResetShortcut", reset); dialog.RegisterName("CancelShortcut", cancel); dialog.RegisterName("SaveShortcut", save);
        panel.Children.Add(actions);

        keys.PreviewMouseDown += (_, _) => keys.Focus();
        keys.PreviewKeyDown += (_, e) => {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape) { dialog.DialogResult = false; e.Handled = true; return; }
            if (key == Key.Tab) return;
            e.Handled = true;
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
            var next = new CaptureHotkey((uint)KeyInterop.VirtualKeyFromKey(key), (uint)Keyboard.Modifiers);
            save.IsEnabled = next.IsValid;
            error.Text = next.IsValid ? "" : "Choose a key together with Ctrl, Alt or Win, or a supported function key.";
            if (next.IsValid) { candidate = next; ShowCandidate(); }
        };
        dialog.Content = panel; dialog.Loaded += (_, _) => keys.Focus();
        // Let the picker receive the currently registered key without starting capture.
        globalShortcut.Suspend();
        try { dialog.ShowDialog(); }
        finally { globalShortcut.TryRegister(); UpdateShortcutButton(); if (Content != editorPage) ShowSettings(); }
    }
}
