using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace SnipCanvas;
internal static partial class SelfTest
{
    public static void EscapeCheck() => EscapeTest.Run();

    /// <summary>Reads a private field or property, so checks keep working when state moves between the two.</summary>
    internal static object? Read(object target, string name)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var field = target.GetType().GetField(name, flags);
        return field != null ? field.GetValue(target) : target.GetType().GetProperty(name, flags)!.GetValue(target);
    }
    public static void EditableTextCheck()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SnipCanvas-editable-text-" + Guid.NewGuid().ToString("N"));
        try
        {
            var data = new byte[600 * 300 * 4]; Array.Fill(data, (byte)255);
            var source = BitmapSource.Create(600, 300, 96, 96, PixelFormats.Bgra32, null, data, 600 * 4);
            var window = new MainWindow { IsHitTestVisible = false }; window.Show();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            object? Invoke(string name, params object[] args) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, args);
            object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
            // Exercise drag state directly without capturing the user's physical mouse during tests.
            ((System.Windows.Controls.Canvas)Field("canvas")!).IsEnabled = false;
            var edits = (System.Collections.Generic.List<ImageEdit>)Field("imageEdits")!;
            void EqualImage(BitmapSource expected, BitmapSource actual)
            {
                Require(expected.PixelWidth == actual.PixelWidth && expected.PixelHeight == actual.PixelHeight, "Rendered image dimensions changed");
                byte[] Read(BitmapSource image) { var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0); var result = new byte[image.PixelWidth * image.PixelHeight * 4]; converted.CopyPixels(result, image.PixelWidth * 4, 0); return result; }
                Require(System.Linq.Enumerable.SequenceEqual(Read(expected), Read(actual)), "Editable text did not match the saved image or left old pixels behind. " + string.Join("; ", edits));
            }
            Invoke("SetImage", source); Invoke("SelectTool", "Text");
            Invoke("CommitEdit", new TextEdit(new Point(40, 30), "Move me", Colors.DodgerBlue, 26));
            Require((bool)Invoke("BeginTextSelection", new Point(50, 40), 1)!, "Text must be selectable");
            Invoke("MoveSelectedText", new Point(110, 80)); Invoke("EndTextMove");
            Require(((TextEdit)edits[0]).Position == new Point(100, 70), "Dragging must move the text by the pointer delta");
            EqualImage(Edits.Text(source, new Point(100, 70), "Move me", Colors.DodgerBlue, 26), (BitmapSource)Field("bitmap")!);
            Invoke("ResizeText", 10.0); Require(((TextEdit)edits[0]).Size == 36, "Text size must increase");
            Invoke("DeleteSelectedText"); Require(edits.Count == 0, "Delete text must remove only the annotation"); EqualImage(source, (BitmapSource)Field("bitmap")!);
            Invoke("Undo"); Require(((TextEdit)edits[0]).Size == 36, "Undo must restore deleted text and size");
            Invoke("Redo"); Require(edits.Count == 0, "Redo must remove text again"); Invoke("Undo");
            Invoke("CommitEdit", new CropEdit(new Int32Rect(80, 50, 400, 200)));
            Require((bool)Invoke("BeginTextSelection", new Point(25, 25), 1)!, "Text selection must follow crop coordinates"); Invoke("EndTextMove");
            Invoke("ResizeText", -4.0); Require(((TextEdit)edits[0]).Size == 32, "Text must remain resizable after crop");
            var expected = new CroppedBitmap(Edits.Text(source, new Point(100, 70), "Move me", Colors.DodgerBlue, 32), new Int32Rect(80, 50, 400, 200));
            EqualImage(expected, (BitmapSource)Field("bitmap")!);
            Invoke("CommitEdit", new BlurEdit(new Int32Rect(15, 15, 100, 45)));
            EqualImage(Edits.Blur(expected, new Int32Rect(15, 15, 100, 45)), (BitmapSource)Field("bitmap")!);
            Invoke("Undo"); Invoke("DeleteImage"); Invoke("UndoDelete"); Require(edits.OfType<TextEdit>().Count() == 1, "Undo image deletion must restore editable text");
            Invoke("BeginTextSelection", new Point(25, 25), 1); Invoke("EndTextMove");
            foreach (bool dark in new[] { true, false })
            {
                window.ChangeTheme(dark); window.UpdateLayout(); var visual = (FrameworkElement)window.Content;
                var rendered = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth + visual.Margin.Left + visual.Margin.Right), (int)Math.Ceiling(visual.ActualHeight + visual.Margin.Top + visual.Margin.Bottom), 96, 96, PixelFormats.Pbgra32);
                var background = new DrawingVisual(); using (var dc = background.RenderOpen()) dc.DrawRectangle(window.Background, null, new Rect(0, 0, rendered.PixelWidth, rendered.PixelHeight));
                rendered.Render(background); rendered.Render(visual);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(rendered)); Directory.CreateDirectory("previews");
                using var file = File.Create("previews/editable-text-" + (dark ? "dark" : "light") + ".png"); encoder.Save(file);
            }
            ((CapturePreferences)Field("preferences")!).SaveFolder = folder;
            window.Quit();
            var files = Directory.GetFiles(folder, "*.png"); Require(files.Length == 1, "Quit must save the edited image once");
            using (var file = File.OpenRead(files[0])) EqualImage(expected, BitmapDecoder.Create(file, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0]);
            File.WriteAllText("editable-text-test-result.txt", "PASS: text selection, dragging without ghost pixels, resizing, deletion, undo/redo, crop coordinates, blur ordering, undo image deletion, light/dark layout, and automatic PNG save without selection outline.");
        }
        catch (Exception ex) { File.WriteAllText("editable-text-test-result.txt", ex.ToString()); Environment.ExitCode = 1; }
        finally { if (Directory.Exists(folder)) { foreach (var file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); } }
    }

    public static void TextCheck()
    {
        try
        {
            var white = new byte[320 * 120 * 4]; Array.Fill(white, (byte)255);
            var source = BitmapSource.Create(320, 120, 96, 96, PixelFormats.Bgra32, null, white, 320 * 4);
            var blue = Edits.Text(source, new Point(10, 10), "Blue text", Colors.Blue);
            var pixels = new byte[white.Length]; blue.CopyPixels(pixels, 320 * 4, 0);
            int bluePixels = 0;
            for (int i = 0; i < pixels.Length; i += 4) if (pixels[i] > 240 && pixels[i + 1] < 20 && pixels[i + 2] < 20) bluePixels++;
            Require(bluePixels > 30, "Chosen text color must render into the screenshot");
            Require(pixels[^4] == 255 && blue.PixelWidth == 320 && blue.PixelHeight == 120, "Text must preserve image bounds and untouched pixels");
            var window = new MainWindow(); window.Show();
            foreach (bool dark in new[] { true, false })
            {
                window.ChangeTheme(dark);
                string previewText = ""; Color previewColor = Colors.Transparent;
                var dialog = new TextDialog(window, window.PointToScreen(new Point(400, 420)), Colors.OrangeRed, (value, color) => { previewText = value; previewColor = color; });
                Exception? failure = null;
                dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(new Action(() => {
                    try
                    {
                        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                        var input = (System.Windows.Controls.TextBox)typeof(TextDialog).GetField("input", flags)!.GetValue(dialog)!;
                        var hex = (System.Windows.Controls.TextBox)typeof(TextDialog).GetField("hex", flags)!.GetValue(dialog)!;
                        var add = (System.Windows.Controls.Button)typeof(TextDialog).GetField("add", flags)!.GetValue(dialog)!;
                        input.Text = "Look here"; hex.Text = "#1E90FF";
                        Require(add.IsEnabled && previewText == "Look here" && previewColor == Colors.DodgerBlue, "Live preview must follow the selected text and color");
                        hex.Text = "# ZZZZZ"; Require(!add.IsEnabled, "Invalid custom colors must not be accepted");
                        hex.Text = "#1E90FF"; dialog.UpdateLayout();
                        Require(dialog.ActualWidth <= 360 && dialog.ActualHeight < 260, "Text panel should stay compact");
                        var visual = (FrameworkElement)dialog.Content;
                        var rendered = new RenderTargetBitmap((int)Math.Ceiling(dialog.ActualWidth), (int)Math.Ceiling(dialog.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                        rendered.Render(visual);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(rendered)); Directory.CreateDirectory("previews");
                        using (var file = File.Create("previews/text-" + (dark ? "dark" : "light") + ".png")) encoder.Save(file);
                        add.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    }
                    catch (Exception ex) { failure = ex; dialog.DialogResult = false; }
                }));
                bool? accepted = dialog.ShowDialog();
                if (failure != null) throw failure;
                Require(accepted == true && dialog.TextColor == Colors.DodgerBlue, "Adding text must retain the selected color");
            }
            window.Quit();
            File.WriteAllText("text-test-result.txt", "PASS: colored text rendering, image bounds, custom color validation, live preview, compact light/dark panel, selected color on apply.");
        }
        catch (Exception ex) { File.WriteAllText("text-test-result.txt", ex.ToString()); Environment.ExitCode = 1; }
    }

    private static BitmapSource AwaitCapture(IntPtr handle)
    {
        var task = GraphicsWindowCapture.TakeAsync(handle);
        while (!task.IsCompleted) { System.Windows.Forms.Application.DoEvents(); System.Threading.Thread.Sleep(10); }
        return task.GetAwaiter().GetResult();
    }

    public static void VideoCheck(IntPtr handle)
    {
        try
        {
            // The fixture must be playing visibly before testing partial occlusion. Chromium may
            // suspend a browser completely covered by unrelated desktop windows.
            SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x13);
            var ready = System.Diagnostics.Stopwatch.StartNew();
            while (ready.ElapsedMilliseconds < 1000) { System.Windows.Forms.Application.DoEvents(); System.Threading.Thread.Sleep(10); }
            void CheckVideo(BitmapSource image)
            {
                Directory.CreateDirectory("previews");
                var diagnostic = new PngBitmapEncoder(); diagnostic.Frames.Add(BitmapFrame.Create(image));
                using (var file = File.Create("previews/video-capture.png")) diagnostic.Save(file);
                var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
                var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
                converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
                int green = 0, red = 0;
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    if (pixels[i + 1] > 200 && pixels[i] < 50 && pixels[i + 2] < 50) green++;
                    if (pixels[i + 2] > 200 && pixels[i] < 50 && pixels[i + 1] < 50) red++;
                }
                Require(green > converted.PixelWidth * converted.PixelHeight / 3, $"Browser video frame was black or missing ({green} green pixels of {converted.PixelWidth * converted.PixelHeight})");
                Require(red < converted.PixelWidth * converted.PixelHeight / 100, "The covering window appeared over the captured video");
            }
            var visible = AwaitCapture(handle); CheckVideo(visible);
            using (var selection = new System.Windows.Forms.Form {
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.None,
                StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                Bounds = new System.Drawing.Rectangle(90, 90, 920, 680),
                BackColor = System.Drawing.Color.Gray, TopMost = true, ShowInTaskbar = false
            })
            {
                selection.Show(); selection.Refresh();
                var selecting = System.Diagnostics.Stopwatch.StartNew();
                while (selecting.ElapsedMilliseconds < 750) { System.Windows.Forms.Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                selection.Close();
            }
            CheckVideo(AwaitCapture(handle));
            using var cover = new System.Windows.Forms.Form {
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.None,
                StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                Bounds = new System.Drawing.Rectangle(150, 150, 600, 400),
                BackColor = System.Drawing.Color.Red, TopMost = true, ShowInTaskbar = false
            };
            cover.Show(); cover.Refresh(); System.Windows.Forms.Application.DoEvents();
            var covered = AwaitCapture(handle); CheckVideo(covered);
            File.WriteAllText("video-test-result.txt", "PASS: H.264 browser video pixels captured, including while another window covers the browser.");
        }
        catch (Exception ex) { File.WriteAllText("video-test-result.txt", ex.ToString()); Environment.ExitCode = 1; }
        finally { SetWindowPos(handle, new IntPtr(-2), 0, 0, 0, 0, 0x13); }
    }

    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);

    public static void WindowCheck()
    {
        try
        {
            using var target = new System.Windows.Forms.Form {
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.None,
                StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                Bounds = new System.Drawing.Rectangle(100, 100, 320, 240),
                BackColor = System.Drawing.Color.Lime, ShowInTaskbar = false
            };
            using var cover = new System.Windows.Forms.Form {
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.None,
                StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                Bounds = new System.Drawing.Rectangle(220, 140, 160, 160),
                BackColor = System.Drawing.Color.Red, TopMost = true, ShowInTaskbar = false
            };
            target.Show(); target.Refresh(); cover.Show(); cover.Refresh();
            System.Windows.Forms.Application.DoEvents();
            using (var desktop = new System.Drawing.Bitmap(1, 1))
            {
                using var graphics = System.Drawing.Graphics.FromImage(desktop);
                graphics.CopyFromScreen(260, 200, 0, 0, new System.Drawing.Size(1, 1));
                Require(desktop.GetPixel(0, 0).R > 240 && desktop.GetPixel(0, 0).G < 10, "Test window must be visibly covered in red");
            }
            var captured = AwaitCapture(target.Handle);
            Require(captured.PixelWidth == 320 && captured.PixelHeight == 240, "Window capture dimensions changed");
            var converted = new FormatConvertedBitmap(captured, PixelFormats.Bgra32, null, 0);
            var pixel = new byte[4]; converted.CopyPixels(new Int32Rect(160, 100, 1, 1), pixel, 4, 0);
            Require(pixel[1] > 240 && pixel[2] < 10 && pixel[3] == 255, "Overlapping window contaminated the capture or alpha was lost");
            cover.Bounds = target.Bounds; cover.Refresh();
            captured = AwaitCapture(target.Handle);
            converted = new FormatConvertedBitmap(captured, PixelFormats.Bgra32, null, 0);
            converted.CopyPixels(new Int32Rect(160, 100, 1, 1), pixel, 4, 0);
            Require(pixel[1] > 240 && pixel[2] < 10, "Fully covered window was not captured independently");
            target.Close();
            bool rejected = false;
            try { AwaitCapture(IntPtr.Zero); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Invalid window must fail without capturing desktop pixels");
            File.WriteAllText("window-test-result.txt", "PASS: partial and full overlap excluded, full window dimensions, opaque pixels, invalid target rejected.");
        }
        catch (Exception ex) { File.WriteAllText("window-test-result.txt", ex.ToString()); Environment.ExitCode = 1; }
    }

    public static void TrayCheck()
    {
        try
        {
            var window = new MainWindow();
            new WindowInteropHelper(window).EnsureHandle();
            window.VerifyShareIntegration();
            var sharePixels = new byte[12 * 8 * 4];
            Array.Fill(sharePixels, (byte)255);
            var shareImage = BitmapSource.Create(12, 8, 96, 96, PixelFormats.Bgra32, null, sharePixels, 12 * 4);
            const System.Reflection.BindingFlags privateInstance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            void InvokeEditor(string name, params object[] args) => typeof(MainWindow).GetMethod(name, privateInstance)!.Invoke(window, args);
            object? EditorField(string name) => Read(window, name);
            InvokeEditor("SetImage", shareImage);
            InvokeEditor("Commit", shareImage);
            InvokeEditor("DeleteImage");
            Require(EditorField("bitmap") == null && !(bool)EditorField("dirty")!, "Delete must clear the editor without scheduling an automatic save");
            InvokeEditor("Redo");
            Require(EditorField("bitmap") == null, "Redo must not restore an empty editor");
            InvokeEditor("Undo");
            Require(ReferenceEquals(EditorField("bitmap"), shareImage) && (bool)EditorField("dirty")!, "Undo delete must restore the image and unsaved state");
            InvokeEditor("Undo");
            InvokeEditor("Redo");
            InvokeEditor("DeleteImage");
            InvokeEditor("SetImage", shareImage);
            Require(ReferenceEquals(EditorField("bitmap"), shareImage), "A new image must open after deletion");
            InvokeEditor("DeleteImage");
            string sharePath = MainWindow.WriteShareImage(shareImage);
            string autoSaveOne = MainWindow.SavePreviousScreenshot(shareImage, Path.GetDirectoryName(sharePath)!);
            string autoSaveTwo = MainWindow.SavePreviousScreenshot(shareImage, Path.GetDirectoryName(sharePath)!);
            try {
                Require(autoSaveOne != autoSaveTwo && File.Exists(autoSaveOne) && File.Exists(autoSaveTwo), "Automatic saves must not overwrite each other");
                using var savedStream = File.OpenRead(autoSaveOne);
                var savedImage = BitmapDecoder.Create(savedStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                Require(savedImage.PixelWidth == 12 && savedImage.PixelHeight == 8, "Automatic save changed image dimensions");
            } finally { File.Delete(autoSaveOne); File.Delete(autoSaveTwo); }
            try {
                using var stream = File.OpenRead(sharePath);
                var decoded = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                Require(decoded.PixelWidth == 12 && decoded.PixelHeight == 8, "Share PNG dimensions changed");
            } finally { File.Delete(sharePath); }
            Require(!window.IsVisible && window.TrayVisible, "Startup should initialize the tray without showing a window");
            window.RestoreFromTray();
            Require(window.IsVisible, "Open should restore the hidden window");
            window.Close();
            Require(!window.IsVisible && window.TrayVisible, "Close should keep the tray active");
            window.RestoreFromTray();
            Require(window.IsVisible, "Closed window should reopen from tray");
            string quitFolder = Path.Combine(Path.GetTempPath(), "SnipCanvas-quit-check-" + Guid.NewGuid().ToString("N"));
            string manualFolder = Path.Combine(quitFolder, "chosen-folder");
            ((CapturePreferences)EditorField("preferences")!).SaveFolder = manualFolder;
            InvokeEditor("SetImage", shareImage);
            InvokeEditor("Commit", new CroppedBitmap(shareImage, new Int32Rect(0, 0, 6, 4)));
            InvokeEditor("Save");
            var manualFiles = Directory.GetFiles(manualFolder, "*.png");
            Require(manualFiles.Length == 1 && !(bool)EditorField("dirty")!, "Save PNG must save directly in the configured folder and clear unsaved state");
            using (var savedStream = File.OpenRead(manualFiles[0]))
            {
                var savedImage = BitmapDecoder.Create(savedStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                Require(savedImage.PixelWidth == 6 && savedImage.PixelHeight == 4, "Manual save must include current edits");
            }
            InvokeEditor("Save");
            Require(Directory.GetFiles(manualFolder, "*.png").Length == 2, "Repeated manual saves must not overwrite an existing screenshot");
            foreach (var file in Directory.GetFiles(manualFolder)) File.Delete(file);
            Directory.Delete(manualFolder);
            ((CapturePreferences)EditorField("preferences")!).SaveFolder = quitFolder;
            InvokeEditor("SetImage", shareImage);
            InvokeEditor("Commit", new CroppedBitmap(shareImage, new Int32Rect(0, 0, 6, 4)));
            window.Close();
            try
            {
                window.Quit();
                var savedFiles = Directory.GetFiles(quitFolder, "*.png");
                Require(savedFiles.Length == 1 && !(bool)EditorField("dirty")!, "Quit must save unsaved edits exactly once");
                using var savedStream = File.OpenRead(savedFiles[0]);
                var savedImage = BitmapDecoder.Create(savedStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                Require(savedImage.PixelWidth == 6 && savedImage.PixelHeight == 4, "Quit must save the edited screenshot, not the original capture");
            }
            finally
            {
                if (Directory.Exists(quitFolder))
                {
                    foreach (var file in Directory.GetFiles(quitFolder)) File.Delete(file);
                    Directory.Delete(quitFolder);
                }
            }
            Require(!window.TrayVisible, "Quit should remove the tray icon");
            Require(StartupSetting.Command == "\"" + Environment.ProcessPath + "\" --tray", "Startup command must quote executable and start in tray");
            File.WriteAllText("tray-test-result.txt", "PASS: Windows share integration, share PNG export, hidden startup, tray icon, close to tray, reopen, automatic save of edited screenshot on quit, quit cleanup, startup command. No image was sent and Windows startup preference was not changed.");
        }
        catch (Exception ex) { File.WriteAllText("tray-test-result.txt", ex.ToString()); Environment.ExitCode = 1; }
    }

    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private static void TestGlobalShortcut()
    {
        using var first = new HwndSource(new HwndSourceParameters("SnipCanvas shortcut test") { ParentWindow = new IntPtr(-3) });
        using var second = new HwndSource(new HwndSourceParameters("SnipCanvas shortcut conflict test") { ParentWindow = new IntPtr(-3) });
        var frame = new DispatcherFrame();
        bool received = false;
        var original = new CaptureHotkey(0x86, 3); // Ctrl+Alt+F23 avoids the running app's default.
        var replacement = new CaptureHotkey(0x87, 3);
        using var shortcut = new GlobalCaptureShortcut(first, () => { received = true; frame.Continue = false; }, original);
        Require(shortcut.Registered, "Test shortcut Ctrl+Alt+F23 is unavailable.");
        using var conflict = new GlobalCaptureShortcut(second, () => { }, original);
        Require(!conflict.Registered, "Duplicate shortcut registration should fail");
        Require(PostMessage(first.Handle, 0x0312, new IntPtr(0x5354), IntPtr.Zero), "Could not queue hotkey message");
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timeout.Tick += (_, _) => frame.Continue = false;
        timeout.Start(); Dispatcher.PushFrame(frame); timeout.Stop();
        Require(received, "WM_HOTKEY did not dispatch capture callback");
        Require(shortcut.TryChange(replacement), "Could not change global shortcut");
        Require(conflict.TryRegister(), "Changing shortcut did not release the old key");
        Require(!shortcut.TryChange(original), "An occupied shortcut should be rejected");
        Require(shortcut.Registered && shortcut.Gesture == replacement, "Conflict lost the previous registration");
        Require(!shortcut.TryChange(new CaptureHotkey(0x41, 0)), "Bare letter must be rejected");
        Require(!shortcut.TryChange(new CaptureHotkey(0x7B, 2)), "Reserved F12 must be rejected");
        Require(CaptureHotkey.Default.Label == "Print Screen" && new CaptureHotkey(0x41, 6).Label == "Ctrl + Shift + A", "Incorrect shortcut labels");
        var saved = System.Text.Json.JsonSerializer.Serialize(new CapturePreferences { Shortcut = replacement });
        Require(System.Text.Json.JsonSerializer.Deserialize<CapturePreferences>(saved)!.Shortcut == replacement, "Shortcut preference did not survive serialization");
        Require(System.Text.Json.JsonSerializer.Deserialize<CapturePreferences>("{}")!.Shortcut == CaptureHotkey.Default, "Old settings must default to Print Screen");
        shortcut.Suspend();
        Require(!shortcut.Registered && conflict.TryChange(replacement), "Picker did not release its shortcut");
        conflict.Dispose();
        Require(shortcut.TryRegister(), "Cancelling the picker did not restore registration");
        shortcut.Dispose();
        using var released = new GlobalCaptureShortcut(second, () => { }, replacement);
        Require(released.Registered, "Shortcut was not released on disposal");
    }

    public static void Run()
    {
        try
        {
            const int w = 120, h = 80;
            byte[] data = new byte[w*h*4];
            for (int y=0;y<h;y++) for(int x=0;x<w;x++) { int i=(y*w+x)*4; data[i]=data[i+1]=data[i+2]=(byte)(x%2==0?0:255); data[i+3]=255; }
            var image = BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,data,w*4);
            var blurred = Edits.Blur(image,new Int32Rect(20,20,60,40));
            byte[] result = new byte[data.Length]; blurred.CopyPixels(result,w*4,0);
            Require(result[0]==data[0],"Blur changed pixels outside selection");
            Require(result[(30*w+40)*4]>80 && result[(30*w+40)*4]<180,"Blur did not average pixels");
            var arrow = Edits.Arrow(image,new Point(10,10),new Point(80,60));
            Require(arrow.PixelWidth==w && arrow.PixelHeight==h,"Arrow changed dimensions");
            arrow.CopyPixels(result,w*4,0);
            Require(!System.Linq.Enumerable.SequenceEqual(data,result),"Arrow did not render");
            var text = Edits.Text(image,new Point(5,5),"Hello");
            text.CopyPixels(result,w*4,0);
            Require(!System.Linq.Enumerable.SequenceEqual(data,result),"Text did not render");
            var crop = new CroppedBitmap(arrow,new Int32Rect(5,5,40,30));
            Require(crop.PixelWidth==40 && crop.PixelHeight==30,"Crop dimensions incorrect");
            using var stream = new MemoryStream(); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(crop)); encoder.Save(stream); stream.Position=0;
            var decoded = BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];
            Require(decoded.PixelWidth==40 && decoded.PixelHeight==30,"PNG roundtrip failed");
            TestGlobalShortcut();
            var window = new MainWindow(); window.Show(); window.UpdateLayout();
            Require(window.ActualWidth>=850,"Window failed to lay out");
            TestShortcutPicker(window);
            window.Close();
            Require(!window.IsVisible, "Close should hide SnipCanvas in the tray");
            window.RestoreFromTray();
            Require(window.IsVisible, "Tray restore should reopen SnipCanvas");
            window.Quit();
            File.WriteAllText("self-test-result.txt","PASS: image editing and PNG roundtrip; shortcut registration, changes, conflict preservation, validation, persistence, suspension, dispatch, release; shortcut picker keyboard input, reset, cancellation, and layout; window layout and tray restore.");
        }
        catch(Exception ex) { File.WriteAllText("self-test-result.txt",ex.ToString()); Environment.ExitCode=1; }
    }
    private static void Require(bool condition,string message) { if(!condition) throw new Exception(message); }

    private static void TestShortcutPicker(MainWindow window)
    {
        Exception? failure = null;
        bool visited = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) => {
            timer.Stop();
            var dialog = window.OwnedWindows.Cast<Window>().Single();
            try {
                var input = (System.Windows.Controls.Border)dialog.FindName("KeyInput");
                var label = (System.Windows.Controls.TextBlock)dialog.FindName("KeyLabel");
                var reset = (System.Windows.Controls.Button)dialog.FindName("ResetShortcut");
                var save = (System.Windows.Controls.Button)dialog.FindName("SaveShortcut");
                void Press(System.Windows.Input.Key key) => input.RaiseEvent(new System.Windows.Input.KeyEventArgs(
                    System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(input), 0, key) {
                        RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent
                    });
                Press(System.Windows.Input.Key.F8);
                Require(label.Text == "F8" && save.IsEnabled, "Picker failed to record a function key");
                Press(System.Windows.Input.Key.A);
                Require(!save.IsEnabled, "Picker allowed an unmodified letter");
                reset.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Require(label.Text == "Print Screen" && save.IsEnabled, "Picker reset failed");
                dialog.UpdateLayout();
                var saveRight = save.TranslatePoint(new Point(save.ActualWidth, 0), dialog).X;
                var resetLeft = reset.TranslatePoint(new Point(0, 0), dialog).X;
                Require(saveRight <= dialog.ActualWidth && resetLeft >= 0, "Shortcut picker buttons overflow");
                var rendered = new RenderTargetBitmap((int)dialog.ActualWidth, (int)dialog.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                rendered.Render(dialog);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(rendered));
                using var file = File.Create("shortcut-picker.png"); encoder.Save(file);
                Press(System.Windows.Input.Key.Escape); visited = true;
            } catch (Exception ex) { failure = ex; dialog.Close(); }
        };
        timer.Start();
        typeof(MainWindow).GetMethod("ChangeCaptureShortcut", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, null);
        timer.Stop();
        if (failure != null) throw failure;
        Require(visited, "Shortcut picker did not open or cancel");
    }
}
