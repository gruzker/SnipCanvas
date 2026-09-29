using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnipCanvas;

/// <summary>Behaviour added by the redesign: recent screenshots, colored marks, resolution-aware sizing, and state shown in the chrome.</summary>
internal static partial class SelfTest
{
    private static byte[] Pixel(BitmapSource image, int x, int y)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var pixel = new byte[4]; converted.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return pixel; // B, G, R, A
    }

    private static BitmapSource Blank(int width, int height, byte value = 255)
    {
        var data = new byte[width * height * 4]; Array.Fill(data, value);
        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, data, width * 4);
    }

    public static void RedesignCheck()
    {
        string root = Path.Combine(Path.GetTempPath(), "SnipCanvas-redesign-" + Guid.NewGuid().ToString("N"));
        MainWindow? window = null;
        try
        {
            Directory.CreateDirectory(root);

            // ---- Recent screenshots
            void Write(string name, int daysAgo)
            {
                string path = Path.Combine(root, name);
                SavePng(Blank(40, 30), path);
                File.SetLastWriteTime(path, DateTime.Now.AddDays(-daysAgo));
            }
            Write("SnipCanvas-2026-01-01-000000-000-a.png", 3);
            Write("SnipCanvas-2026-01-02-000000-000-b.png", 1);
            Write("OtherApp-2025-12-30-000000-000-c.png", 2);
            Write("Holiday.png", 0);
            File.WriteAllText(Path.Combine(root, "notes.txt"), "not an image");
            var found = Recents.Find(root, 10);
            Require(found.Count == 2, "Only screenshots from SnipCanvas belong in Recent screenshots");
            Require(found.Select(f => Path.GetFileName(f.Path)).SequenceEqual(new[] { "SnipCanvas-2026-01-02-000000-000-b.png", "SnipCanvas-2026-01-01-000000-000-a.png" }), "Recent screenshots must be newest first");
            Require(Recents.Find(root, 1).Count == 1, "Recent screenshots must respect the requested count");
            Require(Recents.Find(Path.Combine(root, "missing"), 5).Count == 0, "A missing folder means no recent screenshots, not an error");
            var noon = new DateTime(2026, 9, 29, 12, 0, 0);
            Require(Recents.Describe(noon.AddHours(-2), noon).StartsWith("Today"), "Same-day screenshots read 'Today'");
            Require(Recents.Describe(noon.AddDays(-1), noon).StartsWith("Yesterday"), "Previous-day screenshots read 'Yesterday'");
            Require(!Recents.Describe(noon.AddDays(-5), noon).StartsWith("Today"), "Older screenshots show a date");
            Require(Recents.Load(found[0].Path).PixelWidth == 40, "Opening a recent screenshot must load the full image");
            Require(Recents.Thumbnail(found[0].Path, 20)?.PixelWidth == 20, "Thumbnails are decoded at the requested width");
            Require(Recents.Thumbnail(Path.Combine(root, "notes.txt"), 20) == null, "A file that is not an image gives no thumbnail");

            // ---- Colored marks
            var white = Blank(100, 80);
            var box = Edits.Box(white, new Rect(20, 20, 60, 40), Colors.Blue, 4);
            var onEdge = Pixel(box, 20, 40); var inside = Pixel(box, 50, 40); var outside = Pixel(box, 5, 5);
            Require(onEdge[0] > 200 && onEdge[1] < 60 && onEdge[2] < 60, "A box must draw its outline in the chosen color");
            Require(inside[0] == 255 && inside[1] == 255 && inside[2] == 255 && outside[2] == 255, "A box must not fill its inside or touch pixels outside it");
            var arrow = Edits.Arrow(white, new Point(10, 10), new Point(90, 70), Colors.Green, 6);
            var pixels = new byte[100 * 80 * 4]; arrow.CopyPixels(pixels, 100 * 4, 0);
            int green = 0; for (int i = 0; i < pixels.Length; i += 4) if (pixels[i + 1] > 100 && pixels[i] < 60 && pixels[i + 2] < 60) green++;
            Require(green > 60, "An arrow must be drawn in the chosen color");
            Require(Math.Abs(Edits.ArrowGeometry(new Point(0, 0), new Point(0, 100), 8).Bounds.Width - 36) < 0.01 && Math.Abs(Edits.ArrowGeometry(new Point(0, 0), new Point(0, 100)).Bounds.Width - 18) < 0.01, "Arrowheads must scale with line thickness");
            Require(new ArrowEdit(new Point(0, 0), new Point(5, 5), Colors.Red, 8) != new ArrowEdit(new Point(0, 0), new Point(5, 5), Colors.Blue, 8), "Arrow color is part of the edit");

            // ---- Window behaviour
            window = new MainWindow { IsHitTestVisible = false };
            window.Show();
            object? Get(string name) => Read(window, name);
            void Call(string method, params object[] args) => typeof(MainWindow).GetMethod(method, Private)!.Invoke(window, args);
            var preferences = (CapturePreferences)Get("preferences")!;
            preferences.SaveFolder = root;
            Call("RefreshRecents");
            var grid = (Panel)Get("recentGrid")!;
            for (int i = 0; i < 60 && grid.Children.Count != 2; i++) Pump(50);
            Require(grid.Children.Count == 2, "The home screen must list recent screenshots from the save folder");
            preferences.ShowRecents = false; Call("RefreshRecents");
            Require(((UIElement)Get("recentSection")!).Visibility == Visibility.Collapsed, "Recent screenshots can be switched off");
            preferences.ShowRecents = true;

            // Home vs editor
            var homeView = (UIElement)Get("homeView")!; var editView = (UIElement)Get("editView")!; var commandBar = (UIElement)Get("commandBar")!;
            Require(homeView.Visibility == Visibility.Visible && editView.Visibility == Visibility.Collapsed && commandBar.Visibility == Visibility.Collapsed, "With no screenshot the home screen shows and the toolbar is hidden");
            Call("SetImage", Blank(1600, 900));
            Require(homeView.Visibility == Visibility.Collapsed && editView.Visibility == Visibility.Visible && commandBar.Visibility == Visibility.Visible, "With a screenshot the editor and toolbar show");
            var saveText = (TextBlock)Get("saveText")!;
            Call("Commit", Blank(1600, 900, 200));
            Require(saveText.Text == "Unsaved edits", "Edits must show as unsaved");
            Call("Save");
            Require(saveText.Text == "Saved", "Saving must show the screenshot as saved");

            // Tool selection works for keyboard and automation as well as the mouse
            var tools = (Dictionary<string, RadioButton>)Get("toolButtons")!;
            tools["Blur"].IsChecked = true;
            Require((string)Get("tool")! == "Blur", "Choosing a tool by any means (keyboard, screen reader) must switch the tool");
            var colors = (UIElement)Get("colorOptions")!; var thickness = (UIElement)Get("thicknessOptions")!;
            Require(colors.Visibility == Visibility.Collapsed, "Blur has no color option");
            tools["Box"].IsChecked = true;
            Require(colors.Visibility == Visibility.Visible && thickness.Visibility == Visibility.Visible, "Box offers color and thickness");
            tools["Text"].IsChecked = true;
            Require(colors.Visibility == Visibility.Visible && thickness.Visibility == Visibility.Collapsed, "Text offers color but not line thickness");

            // Every tool shows a live preview while dragging and cleans it up afterwards
            Call("SetImage", Blank(400, 300));
            var canvas = (Canvas)Get("canvas")!;
            foreach (string name in new[] { "Arrow", "Box", "Crop", "Blur" })
            {
                tools[name].IsChecked = true;
                typeof(MainWindow).GetField("start", Private)!.SetValue(window, (Point?)new Point(10, 10));
                int before = canvas.Children.Count;
                canvas.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = System.Windows.Input.Mouse.MouseMoveEvent });
                Require(canvas.Children.Count > before, name + " must show a live preview while dragging");
                Call("CancelDrag");
                Require(canvas.Children.Count == before, name + " preview must disappear when the drag ends");
            }

            // Marks scale with resolution, and stay at a fixed size on small images
            double Thickness() => (double)typeof(MainWindow).GetProperty("AnnotationThickness", Private)!.GetValue(window)!;
            double TextSize() => (double)typeof(MainWindow).GetProperty("DefaultTextSize", Private)!.GetValue(window)!;
            Call("SetImage", Blank(800, 600));
            Require(Thickness() == 5 && TextSize() == 26, "Small screenshots keep the standard mark sizes");
            Call("SetImage", Blank(4000, 2250));
            Require(Thickness() > 12 && TextSize() > 60, "Marks must grow on high-resolution screenshots so they stay visible after fitting to the window");

            // Removing a screenshot offers undo, and the offer goes away once another screenshot is open
            var undoNotice = (UIElement)Get("undoNotice")!;
            Call("DeleteImage");
            Require(undoNotice.Visibility == Visibility.Visible, "Removing a screenshot must offer Undo");
            Call("UndoDelete");
            Require(undoNotice.Visibility == Visibility.Collapsed && Get("bitmap") != null, "Undo brings the screenshot back and hides the offer");
            Call("DeleteImage"); Call("SetImage", Blank(50, 50));
            Require(undoNotice.Visibility == Visibility.Collapsed, "The Undo offer must not linger over a different screenshot");

            // Tray: the icon uses the exact small frame, and every menu entry has an icon that follows the theme
            var trayIcon = (System.Drawing.Icon)typeof(MainWindow).GetMethod("LoadTrayIcon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, null)!;
            Require(trayIcon.Size == System.Windows.Forms.SystemInformation.SmallIconSize, "The tray icon must use the small icon frame, not a scaled-down large one");
            var trayMenu = (System.Windows.Forms.ContextMenuStrip)Get("trayMenu")!;
            var entries = trayMenu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().ToList();
            Require(entries.Count == 7 && trayMenu.Items.Count == 9, "The tray menu keeps its seven entries and two dividers");
            Require(entries.All(item => item.Image != null), "Every tray menu entry has an icon");
            Require(!string.IsNullOrEmpty(entries[1].ShortcutKeyDisplayString), "The tray menu shows the capture shortcut");
            window.ChangeTheme(false); window.ChangeTheme(true);
            Require(entries.All(item => item.Image != null), "Tray menu icons must survive theme changes");

            // Theme
            window.ChangeTheme(true);
            var dark = ((SolidColorBrush)window.Resources["Page"]).Color;
            window.ChangeTheme(false);
            var light = ((SolidColorBrush)window.Resources["Page"]).Color;
            Require(dark.R < 40 && light.R > 200, "Dark and light themes must swap the page color");

            window.AllowSessionExit(); window.Close(); window = null;
            File.WriteAllText("redesign-test-result.txt", "PASS: recent screenshots (filtering, order, count, dates, loading, thumbnails), colored arrows and boxes, resolution-aware mark sizes, home/editor switching, saved and unsaved state, tool selection by keyboard or automation, live drag previews for every tool, tray icon size and menu contents, per-tool options, undo offer lifetime, light/dark palette.");
        }
        catch (Exception ex) { File.WriteAllText("redesign-test-result.txt", ex.ToString()); Environment.ExitCode = 1; }
        finally
        {
            if (window != null) { window.AllowSessionExit(); window.Close(); }
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { }
        }
    }
}
