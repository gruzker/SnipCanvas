using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SnipCanvas;

/// <summary>Renders every screen of the app in both themes to PNG files for inspection. Only synthetic screenshots are used.</summary>
internal static partial class SelfTest
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private static void Pump(int milliseconds = 320)
    {
        var end = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < end)
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            Thread.Sleep(8);
        }
    }

    /// <summary>A made-up web app, so previews look realistic without capturing anyone's desktop.</summary>
    internal static BitmapSource SyntheticScreenshot(int width, int height, int variant = 0)
    {
        Color[] accents = { Color.FromRgb(37, 99, 235), Color.FromRgb(5, 150, 105), Color.FromRgb(217, 119, 6), Color.FromRgb(219, 39, 119), Color.FromRgb(124, 58, 237), Color.FromRgb(8, 145, 178) };
        var accent = new SolidColorBrush(accents[variant % accents.Length]);
        var ink = new SolidColorBrush(Color.FromRgb(30, 41, 59)); var soft = new SolidColorBrush(Color.FromRgb(226, 232, 240));
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(248, 250, 252)), null, new Rect(0, 0, width, height));
            dc.DrawRectangle(accent, null, new Rect(0, 0, width, height * 0.09));
            dc.DrawText(Edits.FormatText("Acme Analytics", Colors.White, height * 0.04), new Point(width * 0.02, height * 0.022));
            dc.DrawRectangle(Brushes.White, null, new Rect(0, height * 0.09, width * 0.17, height * 0.91));
            for (int i = 0; i < 6; i++) dc.DrawRoundedRectangle(i == 1 ? soft : null, null, new Rect(width * 0.015, height * (0.13 + i * 0.07), width * 0.14, height * 0.045), 6, 6);
            dc.DrawText(Edits.FormatText("Quarterly overview", ink.Color, height * 0.05), new Point(width * 0.2, height * 0.13));
            for (int i = 0; i < 3; i++)
            {
                var card = new Rect(width * (0.2 + i * 0.265), height * 0.24, width * 0.25, height * 0.2);
                dc.DrawRoundedRectangle(Brushes.White, new Pen(soft, 1.5), card, 12, 12);
                dc.DrawText(Edits.FormatText(new[] { "Revenue", "Customers", "Churn" }[i], Color.FromRgb(100, 116, 139), height * 0.028), new Point(card.X + 18, card.Y + 14));
                dc.DrawText(Edits.FormatText(new[] { "$48,210", "1,284", "2.4%" }[i], ink.Color, height * 0.062), new Point(card.X + 18, card.Y + height * 0.075));
            }
            var chart = new Rect(width * 0.2, height * 0.5, width * 0.78, height * 0.42);
            dc.DrawRoundedRectangle(Brushes.White, new Pen(soft, 1.5), chart, 12, 12);
            var line = new StreamGeometry();
            using (var c = line.Open())
            {
                c.BeginFigure(new Point(chart.X + 24, chart.Bottom - 30), false, false);
                for (int i = 1; i <= 12; i++) c.LineTo(new Point(chart.X + 24 + i * (chart.Width - 48) / 12, chart.Bottom - 30 - (Math.Sin(i * 0.7 + variant) + 1.4) * chart.Height * 0.26), true, true);
            }
            dc.DrawGeometry(null, new Pen(accent, 4) { LineJoin = PenLineJoin.Round }, line);
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze();
        return bitmap;
    }

    private static void SavePng(BitmapSource image, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path); encoder.Save(file);
    }

    private static BitmapSource Compose(FrameworkElement visual, Brush background, double extraWidth = 0)
    {
        int width = (int)Math.Ceiling(visual.ActualWidth + extraWidth), height = (int)Math.Ceiling(visual.ActualHeight);
        var rendered = new RenderTargetBitmap(Math.Max(1, width), Math.Max(1, height), 96, 96, PixelFormats.Pbgra32);
        var paper = new DrawingVisual();
        using (var dc = paper.RenderOpen()) dc.DrawRectangle(background, null, new Rect(0, 0, rendered.PixelWidth, rendered.PixelHeight));
        rendered.Render(paper); rendered.Render(visual); rendered.Freeze();
        return rendered;
    }

    private static void Snap(MainWindow window, string name)
    {
        Pump(); window.UpdateLayout();
        SavePng(Compose((FrameworkElement)window.Content, window.Background), "previews/" + name + ".png");
    }

    private static void SnapWindow(Window dialog, string name)
    {
        dialog.Show(); Pump(200); dialog.UpdateLayout();
        var content = (FrameworkElement)dialog.Content;
        SavePng(Compose(content, dialog.Background ?? Brushes.Transparent), "previews/" + name + ".png");
        dialog.Close();
    }

    public static void VisualCheck()
    {
        string root = Path.Combine(Path.GetTempPath(), "SnipCanvas-visual-" + Guid.NewGuid().ToString("N"));
        string empty = Path.Combine(root, "empty"), fixtures = Path.Combine(root, "shots");
        try
        {
            Directory.CreateDirectory(empty); Directory.CreateDirectory(fixtures);
            for (int i = 0; i < 6; i++)
            {
                string file = Path.Combine(fixtures, $"SnipCanvas-2026-09-2{i}-1{i}1010-000-{new string((char)('a' + i), 32)}.png");
                SavePng(SyntheticScreenshot(1280, 720, i), file);
                File.SetLastWriteTime(file, DateTime.Now.AddHours(-i * 9).AddMinutes(-i * 7));
            }
            var window = new MainWindow(); window.Show();
            var preferences = (CapturePreferences)Read(window, "preferences")!;
            void Call(string method, params object[] args) => typeof(MainWindow).GetMethod(method, Private)!.Invoke(window, args);
            void WaitForRecents(int expected)
            {
                var grid = (Panel)Read(window, "recentGrid")!;
                for (int i = 0; i < 60 && grid.Children.Count != expected; i++) Pump(50);
            }
            foreach (bool dark in new[] { true, false })
            {
                string mode = dark ? "dark" : "light";
                window.ChangeTheme(dark); window.UpdateLayout();
                Call("HideToast");
                if (Read(window, "bitmap") != null) Call("DeleteImage");
                typeof(MainWindow).GetField("deletedImage", Private)!.SetValue(window, null); Call("RefreshUndoNotice");
                preferences.SaveFolder = empty; Call("RefreshRecents"); WaitForRecents(0);
                Snap(window, "home-empty-" + mode);
                preferences.SaveFolder = fixtures; Call("RefreshRecents"); WaitForRecents(6);
                Snap(window, "home-" + mode);
                window.PreviewShortcutState(false); Snap(window, "home-shortcut-warning-" + mode); window.PreviewShortcutState(true);

                // Editor with each kind of mark
                var shot = SyntheticScreenshot(1600, 900, 0);
                Call("SetImage", shot); Call("SelectTool", "Arrow");
                Call("CommitEdit", new ArrowEdit(new Point(1100, 250), new Point(830, 340), Colors.OrangeRed, 8));
                Call("CommitEdit", new BoxEdit(new Rect(340, 210, 460, 190), Colors.DodgerBlue, 6));
                Call("CommitEdit", new BlurEdit(new Int32Rect(1230, 22, 300, 60)));
                Call("SelectTool", "Text");
                Call("CommitEdit", new TextEdit(new Point(840, 350), "Churn is up!", Colors.OrangeRed, 40));
                Snap(window, "editor-text-" + mode);
                Call("SelectTool", "Arrow"); Snap(window, "editor-arrow-" + mode);
                Call("SelectTool", "Crop"); Snap(window, "editor-crop-" + mode);
                Call("DeleteImage"); Snap(window, "removed-" + mode);
                Call("UndoDelete");

                window.Width = window.MinWidth; window.Height = window.MinHeight + 60; Call("SelectTool", "Text"); Snap(window, "editor-narrow-" + mode);
                window.Width = 1080; window.Height = 760;
                window.RenderShareMenuPreview("previews/share-" + mode + ".png");
                window.RenderMenuPreview(window.CaptureMenu, "previews/capture-menu-" + mode + ".png");
                window.PreviewToast("Saved to SnipCanvas");
                Snap(window, "toast-" + mode);

                window.ShowSettings(); Snap(window, "settings-" + mode);
                window.ShowEditor();
            }
            window.Quit();
            File.WriteAllText("visual-check-result.txt", "PASS: rendered home, recents, warning, editor tools, menus, toast and settings in light and dark.");
        }
        catch (Exception ex) { File.WriteAllText("visual-check-error.txt", ex.ToString()); Environment.ExitCode = 1; }
        finally { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { } }
    }

    /// <summary>Runs the real window against synthetic data so it can be inspected on screen without touching user files.
    /// Usage: --demo [dark|light|system] [editor] [seconds=N]. Preferences are never written unless changed from the UI.</summary>
    public static void Demo(Application app, string[] args)
    {
        string folder = Path.Combine(Path.GetTempPath(), "SnipCanvas-demo");
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        for (int i = 0; i < 6; i++)
        {
            string file = Path.Combine(folder, $"SnipCanvas-2026-09-2{i}-1{i}1010-000-{new string((char)('a' + i), 32)}.png");
            SavePng(SyntheticScreenshot(1280, 720, i), file);
            File.SetLastWriteTime(file, DateTime.Now.AddHours(-i * 9).AddMinutes(-i * 7));
        }
        var window = new MainWindow();
        ((CapturePreferences)Read(window, "preferences")!).SaveFolder = folder;
        if (Array.Exists(args, a => a == "dark")) window.ChangeTheme(true);
        else if (Array.Exists(args, a => a == "light")) window.ChangeTheme(false);
        void Call(string method, params object[] values) => typeof(MainWindow).GetMethod(method, Private)!.Invoke(window, values);
        Call("RefreshRecents");
        if (Array.Exists(args, a => a == "editor"))
        {
            Call("SetImage", SyntheticScreenshot(1600, 900, 0));
            Call("CommitEdit", new ArrowEdit(new Point(1100, 250), new Point(830, 340), Colors.OrangeRed, 8));
            Call("CommitEdit", new BoxEdit(new Rect(340, 210, 460, 190), Colors.DodgerBlue, 6));
            Call("SelectTool", "Arrow");
        }
        // Optional scenes for the separate windows (dialogs, toast, tray menu, capture overlay).
        void Later(Action action) => window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(action, DispatcherPriority.ApplicationIdle);
        if (Array.Exists(args, a => a == "problem"))
            Later(() => Dialogs.Problem(window, "Your screenshot couldn't be saved", "It's still open in the editor, so nothing is lost. Choose another screenshot folder in Settings, then click Save.", "Access to the path is denied."));
        if (Array.Exists(args, a => a == "textdialog"))
            Later(() => new TextDialog(window, window.PointToScreen(new Point(420, 420)), Colors.OrangeRed, (_, _) => { }, "").ShowDialog());
        if (Array.Exists(args, a => a == "shortcut"))
            Later(() => Call("ChangeCaptureShortcut"));
        if (Array.Exists(args, a => a == "preview"))
            Later(() => Call("ShowCapturePreview", SyntheticScreenshot(1600, 900, 2), true));
        if (Array.Exists(args, a => a == "traymenu"))
            Later(() => {
                var menu = (System.Windows.Forms.ContextMenuStrip)Read(window, "trayMenu")!;
                string output = args.FirstOrDefault(a => a.StartsWith("out="))?[4..] ?? Path.Combine(Path.GetTempPath(), "SnipCanvas-traymenu.png");
                menu.Show(new System.Drawing.Point(200, 200));
                menu.Update();
                if (Array.Exists(args, a => a == "live"))
                {
                    // Keep the real popup on screen (it normally closes when the app is not the active window) so it can be inspected.
                    menu.Closing += (_, e) => { if (e.CloseReason != System.Windows.Forms.ToolStripDropDownCloseReason.AppClicked) e.Cancel = true; };
                    menu.Items[1].Select();   // show the highlighted state too
                    return;
                }
                using var image = new System.Drawing.Bitmap(menu.Width, menu.Height);
                menu.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, menu.Size));
                image.Save(output, System.Drawing.Imaging.ImageFormat.Png);
                menu.Close();
            });
        if (Array.Exists(args, a => a == "tooltip"))
            Later(() => {
                var target = (FrameworkElement)Read(window, "undoButton")!;
                _ = new ToolTip { Content = "Undo  (Ctrl+Z)", PlacementTarget = target, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom, IsOpen = true };
            });
        if (Array.Exists(args, a => a == "overlay"))
            Later(() => {
                // Rendered in-process from the overlay's own event handlers: no screen capture and no synthetic input.
                var bounds = new System.Drawing.Rectangle(0, 0, 1920, 1080);
                using var stream = new MemoryStream();
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(SyntheticScreenshot(bounds.Width, bounds.Height, 4))); encoder.Save(stream);
                stream.Position = 0;
                using var desktop = new System.Drawing.Bitmap(stream);
                var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                var targets = new System.Collections.Generic.List<WindowTarget> { new(handle, new System.Drawing.Rectangle(320, 216, 1280, 648)) };
                string output = args.FirstOrDefault(a => a.StartsWith("out="))?[4..] ?? Path.Combine(Path.GetTempPath(), "SnipCanvas-overlay");
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                void Send(System.Windows.Forms.Form form, string handler, System.Windows.Forms.MouseButtons button, int x, int y) =>
                    typeof(System.Windows.Forms.Control).GetMethod(handler, flags)!.Invoke(form, new object[] { new System.Windows.Forms.MouseEventArgs(button, 1, x, y, 0) });
                void Draw(System.Windows.Forms.Form form, string name)
                {
                    using var image = new System.Drawing.Bitmap(form.ClientSize.Width, form.ClientSize.Height);
                    form.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, form.ClientSize));
                    image.Save(output + "-" + name + ".png", System.Drawing.Imaging.ImageFormat.Png);
                }
                using (var area = new SelectionOverlay(desktop, bounds, new(), false))
                {
                    area.Show();
                    Send(area, "OnMouseMove", System.Windows.Forms.MouseButtons.None, 500, 300);
                    Draw(area, "area-idle");
                    Send(area, "OnMouseDown", System.Windows.Forms.MouseButtons.Left, 500, 300);
                    Send(area, "OnMouseMove", System.Windows.Forms.MouseButtons.Left, 1240, 760);
                    Draw(area, "area-drag");
                    area.Close();
                }
                using (var pick = new SelectionOverlay(desktop, bounds, targets, true))
                {
                    pick.Show();
                    Send(pick, "OnMouseMove", System.Windows.Forms.MouseButtons.None, 900, 500);
                    Draw(pick, "window-hover");
                    pick.Close();
                }
                window.Quit();
            });
        int seconds = 60;
        foreach (string arg in args) if (arg.StartsWith("seconds=")) seconds = int.Parse(arg[8..]);
        app.MainWindow = window; app.ShutdownMode = ShutdownMode.OnMainWindowClose;
        var quit = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        quit.Tick += (_, _) => { quit.Stop(); window.Quit(); };
        quit.Start();
        window.Show();
        app.Run();
        try { Directory.Delete(folder, true); } catch (IOException) { }
    }
}
