using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SnipCanvas;

internal sealed class CapturePreferences
{
    public bool AutoCopy { get; set; } = true;
    public bool ShowRecents { get; set; } = true;
    public CaptureHotkey Shortcut { get; set; } = CaptureHotkey.Default;
    public string SaveFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SnipCanvas");
}

internal sealed partial class MainWindow
{
    private readonly CapturePreferences preferences = LoadPreferences();
    private static string PreferencesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnipCanvas", "settings.json");
    private static CapturePreferences LoadPreferences()
    {
        try {
            var value = JsonSerializer.Deserialize<CapturePreferences>(File.ReadAllText(PreferencesPath));
            if (value != null && !string.IsNullOrWhiteSpace(value.SaveFolder) && Path.IsPathFullyQualified(value.SaveFolder)) {
                if (!value.Shortcut.IsValid) value.Shortcut = CaptureHotkey.Default;
                return value;
            }
        } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is ArgumentException) { }
        return new CapturePreferences();
    }

    private bool SavePreferences()
    {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
            string temp = PreferencesPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, PreferencesPath, true); return true;
        } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
            Dialogs.Problem(this, "Your settings couldn't be saved", "The change works for now but will be lost when you close SnipCanvas.", ex.Message); return false;
        }
    }

    internal void ShowEditor() => Content = editorPage;

    internal void ShowSettings()
    {
        if (busy) return;
        RestoreFromTray(); CancelDrag();
        var page = new Grid();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition());

        var header = new DockPanel { Margin = new Thickness(14, 0, 20, 0), Height = 60 };
        var back = Ui.Styled("IconButton", Ui.Icon("chevron-left", 20), ShowEditor, "Back  (Esc)", "Back");
        DockPanel.SetDock(back, Dock.Left); header.Children.Add(back);
        var title = Ui.Text("Settings", 19, "Ink", FontWeights.SemiBold); title.Margin = new Thickness(6, 0, 0, 0); header.Children.Add(title);
        page.Children.Add(header);

        var column = new StackPanel { MaxWidth = 700, Margin = new Thickness(24, 0, 24, 32) };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = column };
        Grid.SetRow(scroll, 1); page.Children.Add(scroll);

        StackPanel Section(string name)
        {
            var heading = Ui.Text(name, 12.5, "Muted", FontWeights.SemiBold); heading.Margin = new Thickness(6, column.Children.Count == 0 ? 4 : 26, 0, 8);
            column.Children.Add(heading);
            var rows = new StackPanel();
            column.Children.Add(Ui.Card(rows));
            return rows;
        }
        void Note(string text)
        {
            var note = Ui.Text(text, 12, "Subtle", wrap: true); note.Margin = new Thickness(6, 8, 6, 0); column.Children.Add(note);
        }
        void Row(StackPanel group, string name, string detail, FrameworkElement control, bool trimDetail = false)
        {
            if (group.Children.Count > 0)
            {
                var line = new Border { Height = 1, Margin = new Thickness(20, 0, 20, 0) };
                line.SetResourceReference(Border.BackgroundProperty, "Line"); group.Children.Add(line);
            }
            var row = new Grid { Margin = new Thickness(20, 16, 20, 16) };
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel { Margin = new Thickness(0, 0, 24, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(Ui.Text(name, 14, "Ink", FontWeights.SemiBold));
            var description = Ui.Text(detail, 12.5, "Muted", wrap: !trimDetail); description.Margin = new Thickness(0, 3, 0, 0);
            if (trimDetail) { description.TextTrimming = TextTrimming.CharacterEllipsis; description.ToolTip = detail; }
            text.Children.Add(description);
            control.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(control, 1);
            row.Children.Add(text); row.Children.Add(control); group.Children.Add(row);
        }
        CheckBox Switch(bool on, Func<bool, bool> apply, string label)
        {
            var toggle = new CheckBox { IsChecked = on };
            toggle.SetResourceReference(FrameworkElement.StyleProperty, "Switch");
            System.Windows.Automation.AutomationProperties.SetName(toggle, label);
            toggle.Click += (_, _) => toggle.IsChecked = apply(toggle.IsChecked == true);
            return toggle;
        }

        var capture = Section("Capture");
        bool active = globalShortcut?.Registered == true;
        var shortcutControls = new StackPanel { Orientation = Orientation.Horizontal };
        var caps = Ui.Kbd(preferences.Shortcut.Label); caps.Margin = new Thickness(0, 0, 12, 0); shortcutControls.Children.Add(caps);
        if (!active)
        {
            var retry = Ui.Styled("Button", "Try again", () => { globalShortcut?.TryRegister(); UpdateShortcutButton(); ShowSettings(); }, "Check the shortcut again");
            retry.Margin = new Thickness(0, 0, 8, 0); shortcutControls.Children.Add(retry);
        }
        shortcutControls.Children.Add(Ui.Styled("Button", "Change", ChangeCaptureShortcut, "Choose a different capture shortcut"));
        Row(capture, "Capture shortcut", active ? "Works in any app, even when SnipCanvas is closed to the tray." : "Not available right now. Another app or Windows may be using it.", shortcutControls);
        Row(capture, "Copy to clipboard", "Put every new screenshot on your clipboard so you can paste it straight away.",
            Switch(preferences.AutoCopy, on => {
                bool before = preferences.AutoCopy; preferences.AutoCopy = on;
                if (!SavePreferences()) preferences.AutoCopy = before;
                return preferences.AutoCopy;
            }, "Copy new screenshots to the clipboard"));
        var folderActions = new StackPanel { Orientation = Orientation.Horizontal };
        folderActions.Children.Add(Ui.Styled("Button", "Open", OpenScreenshotFolder, "Open the folder in File Explorer"));
        var choose = Ui.Styled("Button", "Change…", () => {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose screenshot folder", InitialDirectory = preferences.SaveFolder };
            if (dialog.ShowDialog(this) != true) return;
            string previous = preferences.SaveFolder; preferences.SaveFolder = dialog.FolderName;
            if (!SavePreferences()) preferences.SaveFolder = previous;
            RefreshRecents(); ShowSettings();
        }, "Choose where screenshots are saved");
        choose.Margin = new Thickness(8, 0, 0, 0); folderActions.Children.Add(choose);
        Row(capture, "Screenshot folder", preferences.SaveFolder, folderActions, trimDetail: true);
        Row(capture, "Show recent screenshots", "Show thumbnails of your latest screenshots on the home screen.",
            Switch(preferences.ShowRecents, on => {
                bool before = preferences.ShowRecents; preferences.ShowRecents = on;
                if (!SavePreferences()) preferences.ShowRecents = before;
                RefreshRecents(); return preferences.ShowRecents;
            }, "Show recent screenshots"));
        Note("Every screenshot is saved to the folder above automatically. Edits are saved before your next capture or when you quit.");

        var appearance = Section("Appearance");
        var themes = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (mode, label) in new[] { ("system", "System"), ("dark", "Dark"), ("light", "Light") })
        {
            var choice = new RadioButton { GroupName = "theme", Content = label, IsChecked = themeMode == mode };
            choice.SetResourceReference(FrameworkElement.StyleProperty, "SegmentItem");
            var selected = mode; choice.Click += (_, _) => SetThemeMode(selected, true);
            themes.Children.Add(choice);
        }
        var themeTrack = new Border { CornerRadius = new CornerRadius(11), Padding = new Thickness(2), Child = themes };
        themeTrack.SetResourceReference(Border.BackgroundProperty, "Field");
        Row(appearance, "Theme", "System follows your Windows setting. Screenshot colors are never changed.", themeTrack);

        var system = Section("Windows");
        var startup = Switch(startupEnabled, on => { SetStartup(on); return startupEnabled; }, "Start with Windows");
        if (!startupReadable) { startup.IsEnabled = false; startup.ToolTip = "Windows didn't allow SnipCanvas to read the startup setting."; }
        Row(system, "Start with Windows", "Open quietly in the system tray when you sign in.", startup);
        Row(system, "Quit SnipCanvas", "Closing the window keeps SnipCanvas in the tray so your shortcut keeps working. Quit to exit completely.",
            Ui.Styled("Button", Ui.IconLabel("power", "Quit", out _, 16), Quit, "Save any edits and exit SnipCanvas", "Quit SnipCanvas"));

        var version = typeof(MainWindow).Assembly.GetName().Version;
        var about = Ui.Text($"SnipCanvas {version?.Major}.{version?.Minor}.{version?.Build}  ·  Screenshots are captured and edited on this computer. Nothing is uploaded.", 12, "Subtle", wrap: true);
        about.Margin = new Thickness(6, 28, 6, 0); about.TextAlignment = TextAlignment.Center; column.Children.Add(about);
        Content = page;
    }
}
