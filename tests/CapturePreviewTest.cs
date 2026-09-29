using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SnipCanvas;

internal static class CapturePreviewTest
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

    internal static void Run()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SnipCanvas-preview-" + Guid.NewGuid().ToString("N"));
        MainWindow? window = null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        void Await(Task task)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            timer.Tick += (_, _) => { if (task.IsCompleted) frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame); timer.Stop(); task.GetAwaiter().GetResult();
        }
        try
        {
            window = new MainWindow();
            object? Field(string name) => SelfTest.Read(window, name);
            object? Invoke(string name, params object[] args) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, args);
            var preferences = (CapturePreferences)Field("preferences")!;
            preferences.SaveFolder = folder; preferences.AutoCopy = false;
            new WindowInteropHelper(window).EnsureHandle();
            IntPtr foreground = GetForegroundWindow();
            Await((Task)Invoke("Take", "Screen")!);
            Check(!window.IsVisible, "Capture from tray opened the editor");
            Check(!(bool)Field("dirty")!, "Captured image was not marked saved");
            var files = Directory.GetFiles(folder, "*.png");
            Check(files.Length == 1, "Capture must save immediately");
            using (var file = File.OpenRead(files[0]))
            {
                var saved = BitmapDecoder.Create(file, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                var captured = (BitmapSource)Field("bitmap")!;
                Check(saved.PixelWidth == captured.PixelWidth && saved.PixelHeight == captured.PixelHeight, "Saved image dimensions differ");
            }
            var preview = (CapturePreview)Field("capturePreview")!;
            Check(preview.IsVisible && !preview.ShowInTaskbar && !preview.ShowActivated, "Preview visibility or activation settings incorrect");
            Check(GetForegroundWindow() == foreground, "Capture preview stole foreground focus");
            var area = SystemParameters.WorkArea;
            Check(Math.Abs(preview.Left + preview.Width + 16 - area.Right) < 2 &&
                Math.Abs(preview.Top + preview.Height + 16 - area.Bottom) < 2, "Preview is not at bottom right above the taskbar");
            // Render a synthetic image only, so visual test artifacts contain no desktop data.
            var sample = new RenderTargetBitmap(480, 280, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(0, 0, 480, 280));
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(93, 91, 231)), null, new Rect(0, 0, 480, 48));
                for (int i = 0; i < 3; i++)
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb((byte)(210 + i * 10), 218, 242)), null,
                        new Rect(20 + i * 152, 68, 136, 100), 8, 8);
                dc.DrawRoundedRectangle(Brushes.LightGray, null, new Rect(20, 190, 310, 12), 6, 6);
                dc.DrawRoundedRectangle(Brushes.LightGray, null, new Rect(20, 216, 420, 12), 6, 6);
            }
            sample.Render(visual);
            ((Image)preview.FindName("ScreenshotThumbnail")).Source = sample;
            Await(Task.Delay(300));
            var card = (Border)preview.Content;
            Check(Math.Abs(((TranslateTransform)card.RenderTransform).Y) < 0.01 && Math.Abs(card.Opacity - 1) < 0.01,
                "Entrance animation did not settle into place");
            Directory.CreateDirectory("previews");
            foreach (bool useDark in new[] { false, true })
            {
                window.ChangeTheme(useDark);
                preview.UpdateLayout();
                Check(card.Background == window.Resources["Surface"] && card.BorderBrush == window.Resources["Line"],
                    "Visible preview did not follow the app theme");
                var closeIcon = (System.Windows.Shapes.Path)((Canvas)((Viewbox)((Button)preview.FindName("DismissPreview")).Content).Child).Children[0];
                Check(closeIcon.Stroke == window.Resources["Muted"], "Close button did not follow the app theme");
                Check(ReferenceEquals(((Image)preview.FindName("ScreenshotThumbnail")).Source, sample),
                    "Theme change modified the screenshot");
                var rendered = new RenderTargetBitmap((int)preview.Width, (int)preview.Height, 96, 96, PixelFormats.Pbgra32);
                rendered.Render(preview);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(rendered));
                using var file = File.Create("previews/capture-preview-" + (useDark ? "dark" : "light") + ".png");
                encoder.Save(file);
            }
            Await(Task.Delay(2400)); Check(preview.IsVisible, "Preview disappeared too soon");
            Await(Task.Delay(900)); Check(!preview.IsVisible && Field("capturePreview") == null, "Preview did not disappear after three seconds");
            Check(!window.IsVisible && File.Exists(files[0]), "Timeout opened editor or removed saved image");

            void CancelCapture(bool visible)
            {
                if (visible) window.RestoreFromTray();
                var cancel = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                cancel.Tick += (_, _) => {
                    foreach (System.Windows.Forms.Form form in System.Windows.Forms.Application.OpenForms)
                        if (form is SelectionOverlay overlay)
                        {
                            var message = new System.Windows.Forms.Message();
                            typeof(SelectionOverlay).GetMethod("ProcessCmdKey", flags)!.Invoke(overlay,
                                new object[] { message, System.Windows.Forms.Keys.Escape });
                            break;
                        }
                };
                cancel.Start();
                try { Await((Task)Invoke("Take", "Area")!); }
                finally { cancel.Stop(); }
                Check(window.IsVisible == visible, "Cancellation changed the prior editor visibility");
                Check(Field("capturePreview") == null && Directory.GetFiles(folder, "*.png").Length == 1,
                    "Cancellation saved an image or showed a preview");
                window.Hide();
            }
            CancelCapture(false); CancelCapture(true);

            window.ChangeTheme(false);
            Await((Task)Invoke("Take", "Screen")!);
            var oldPreview = (CapturePreview)Field("capturePreview")!;
            Check(((Border)oldPreview.Content).Background == window.Resources["Surface"], "New preview did not use the selected light theme");
            Await((Task)Invoke("Take", "Screen")!);
            Check(!oldPreview.IsVisible, "Next capture left old preview visible");
            preview = (CapturePreview)Field("capturePreview")!;
            ((Button)preview.FindName("OpenScreenshot")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Check(window.IsVisible && !preview.IsVisible, "Click did not open editor and dismiss preview");
            Check(ReferenceEquals(window.Content, Field("editorPage")), "Preview click did not show editor page");
            Invoke("Commit", new CroppedBitmap((BitmapSource)Field("bitmap")!, new Int32Rect(0, 0, 20, 20)));
            Await((Task)Invoke("Take", "Screen")!);
            Check(!window.IsVisible, "Capture from visible app reopened editor");
            Check(Directory.GetFiles(folder, "*.png").Length == 5, "Repeated captures lost files or failed to save previous edits");
            preview = (CapturePreview)Field("capturePreview")!;
            ((Button)preview.FindName("DismissPreview")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Check(!preview.IsVisible && Field("capturePreview") == null && !window.IsVisible,
                "Close button must dismiss the preview without opening the editor");
            Check(Directory.GetFiles(folder, "*.png").Length == 5, "Dismissal changed saved screenshots");
            window.Quit();
            Check(Field("capturePreview") == null, "Quit left preview running");
            Check(Directory.GetFiles(folder, "*.png").Length == 5, "Quit duplicated an already saved capture");
            window = null;
            File.WriteAllText("capture-preview-test-result.txt", "PASS: immediate PNG save, background capture from hidden and visible app, no foreground focus theft, bottom-right placement, light/dark theme on creation and while visible, unchanged screenshot colors, animation settles, three-second timeout, click-to-open editor, close button dismisses without opening editor or affecting saved files, rapid capture replacement, cancellation preserves hidden/visible state, previous edits saved, no duplicate saves, and quit cleanup.");
        }
        catch (Exception ex) { File.WriteAllText("capture-preview-test-result.txt", ex.ToString()); Environment.ExitCode = 1; }
        finally
        {
            if (window != null) { window.AllowSessionExit(); window.Close(); }
            if (Directory.Exists(folder)) { foreach (var file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
        }
    }
}
