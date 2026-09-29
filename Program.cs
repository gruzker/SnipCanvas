using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SnipCanvas;

internal static class Program
{
    [STAThread] public static void Main(string[] args)
    {
        var app = new Application();
        app.DispatcherUnhandledException += (_, e) => { MessageBox.Show("SnipCanvas could not complete that action.\n\nDetails: " + e.Exception.Message, "SnipCanvas"); e.Handled = true; };
#if SNIPCANVAS_DIAGNOSTICS
        if (Array.Exists(args, a => a == "--self-test")) { SelfTest.Run(); return; }
        if (Array.Exists(args, a => a == "--visual-check")) { SelfTest.VisualCheck(); return; }
        if (Array.Exists(args, a => a == "--tray-check")) { SelfTest.TrayCheck(); return; }
        if (Array.Exists(args, a => a == "--text-check")) { SelfTest.TextCheck(); return; }
        if (Array.Exists(args, a => a == "--editable-text-check")) { SelfTest.EditableTextCheck(); return; }
        if (Array.Exists(args, a => a == "--window-check")) { SelfTest.WindowCheck(); return; }
        if (Array.Exists(args, a => a == "--escape-check")) { SelfTest.EscapeCheck(); return; }
        if (Array.Exists(args, a => a == "--capture-preview-check")) { CapturePreviewTest.Run(); return; }
        if (Array.Exists(args, a => a == "--redesign-check")) { SelfTest.RedesignCheck(); return; }
        if (Array.Exists(args, a => a == "--demo")) { SelfTest.Demo(app, args); return; }
        if (args.Length == 2 && args[0] == "--video-check") { SelfTest.VideoCheck(new IntPtr(long.Parse(args[1], CultureInfo.InvariantCulture))); return; }
#endif
        var window = new MainWindow();
        app.MainWindow = window;
        app.ShutdownMode = ShutdownMode.OnMainWindowClose;
        app.SessionEnding += (_, _) => window.AllowSessionExit();
        if (Array.Exists(args, a => a == "--tray")) new WindowInteropHelper(window).EnsureHandle();
        else window.Show();
        app.Run();
    }
}

internal sealed partial class MainWindow : Window
{
    private readonly Grid stage = new();
    private readonly Canvas canvas = new() { Background = Brushes.White, ClipToBounds = true };
    private readonly Image photo = new() { Stretch = Stretch.Fill };
    private readonly TextBlock status = new();
    private readonly StackPanel toolbar = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel exportsPanel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Stack<EditorState> undo = new();
    private readonly Stack<EditorState> redo = new();
    private readonly Dictionary<string, RadioButton> toolButtons = new();
    private BitmapSource? bitmap;
    private string tool = "Arrow";
    private Color annotationColor = Colors.OrangeRed;
    private Point? start;
    private Shape? preview;
    private Shape? previewOutline;
    private bool busy;
    private bool dirtyValue;
    private bool dirty { get => dirtyValue; set { dirtyValue = value; UpdateSaveState(); } }
    private GlobalCaptureShortcut? globalShortcut;
    private Button shortcutButton = null!;
    private Button undoDeleteButton = null!;
    private BitmapSource? deletedImage;
    private bool deletedImageWasDirty;

    public MainWindow()
    {
        BuildInterface();
        RenderOptions.SetBitmapScalingMode(photo, BitmapScalingMode.HighQuality);
        canvas.Children.Add(photo);
        canvas.MouseLeftButtonDown += Begin; canvas.MouseMove += Move; canvas.MouseLeftButtonUp += End;
        canvas.LostMouseCapture += (_, _) => CancelDrag();
        canvas.QueryCursor += (_, e) => { e.Cursor = CanvasCursor(Mouse.GetPosition(canvas)); e.Handled = true; };
        PreviewKeyDown += async (_, e) => {
            bool onEditor = Content == editorPage;
            if (!onEditor && e.Key == Key.Escape && !e.Handled) { ShowEditor(); e.Handled = true; return; }
            if (onEditor)
            {
                if (e.Key == Key.Escape) { CancelDrag(); selectedText = -1; UpdateTextSelection(); }
                if (e.Key == Key.Delete && selectedText >= 0) { DeleteSelectedText(); e.Handled = true; return; }
                if (Keyboard.Modifiers == ModifierKeys.None && bitmap != null)
                {
                    string? picked = e.Key switch { Key.A => "Arrow", Key.R => "Box", Key.T => "Text", Key.B => "Blur", Key.C => "Crop", _ => null };
                    if (picked != null) { SelectTool(picked); e.Handled = true; return; }
                }
                if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Z) { Redo(); e.Handled = true; return; }
            }
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            switch (e.Key) { case Key.N: await Take("Area"); break; case Key.C: Copy(); break; case Key.S: Save(); break; case Key.Z: Undo(); break; case Key.Y: Redo(); break; case Key.OemComma: ShowSettings(); break; default: return; } e.Handled = true;
        };
        Closing += HandleClosing;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += FollowSystemTheme;
        SourceInitialized += (_, _) => {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)!;
            globalShortcut = new GlobalCaptureShortcut(source, async () => { if (IsEnabled) await Take("Area"); }, preferences.Shortcut);
            ApplyTitleTheme();
            InitializeTray();
            UpdateShortcutButton();
        };
        Closed += (_, _) => {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= FollowSystemTheme;
            DismissCapturePreview(); globalShortcut?.Dispose(); DisposeTray(); DisposeSharing();
        };
    }

    private async Task Take(string mode)
    {
        if (busy || !IsEnabled) return;
        busy = true;
        bool restoreOnCancel = IsVisible && WindowState != WindowState.Minimized;
        bool completed = false;
        DismissCapturePreview();
        CancelDrag();
        try {
            if (!FlushUnsavedEdits("the new capture hasn't started")) return;
            BitmapSource? captured;
            using (var cancellation = new CaptureCancellation())
            {
                Hide();
                await Task.Delay(240, cancellation.Token);
                captured = await Capture.Take(mode, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
            }
            if (captured != null)
            {
                completed = true;
                undo.Clear(); redo.Clear(); SetImage(captured); dirty = true;
                deletedImage = null; deletedState = null; RefreshUndoNotice();
                try
                {
                    captured.Freeze();
                    string folder = preferences.SaveFolder;
                    lastSavedPath = await Task.Run(() => SavePreviousScreenshot(captured, folder));
                    dirty = false;
                }
                catch (Exception ex)
                {
                    ShowEditor(); Show(); Activate();
                    Dialogs.Problem(this, "Your screenshot couldn't be saved", "It's still open in the editor, so nothing is lost. Choose another screenshot folder in Settings, then click Save.", ex.Message);
                    return;
                }
                bool copied = false;
                try
                {
                    if (preferences.AutoCopy) { Clipboard.SetImage(captured); copied = true; }
                    status.Text = "";
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    status.Text = "Captured, but the clipboard is busy. Click Copy to try again.";
                }
                ShowEditor();
                ShowCapturePreview(captured, copied);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Dialogs.Problem(this, "That capture didn't work", "Nothing was changed. Please try again.", ex.Message); }
        finally { busy = false; if (!completed && restoreOnCancel) { Show(); Activate(); } }
    }

    /// <summary>Saves edits made to the current screenshot before it is replaced; false means the replacement must not proceed.</summary>
    private bool FlushUnsavedEdits(string consequence)
    {
        if (!dirty || bitmap == null) return true;
        try
        {
            lastSavedPath = SavePreviousScreenshot(bitmap, preferences.SaveFolder);
            dirty = false;
            status.Text = "Your previous screenshot was saved.";
            return true;
        }
        catch (Exception ex)
        {
            ShowEditor(); Show(); Activate();
            Dialogs.Problem(this, "Your edits couldn't be saved", $"The current screenshot is still open, and {consequence}. Choose another screenshot folder in Settings, then try again.", ex.Message);
            return false;
        }
    }

    private void OpenRecent(RecentItem item)
    {
        if (busy || !FlushUnsavedEdits("that screenshot wasn't opened")) return;
        try
        {
            var image = Recents.Load(item.Path);
            undo.Clear(); redo.Clear(); SetImage(image); dirty = false;
            deletedImage = null; deletedState = null; RefreshUndoNotice();
            ShowEditor();
            status.Text = System.IO.Path.GetFileName(item.Path);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is InvalidOperationException || ex is FormatException)
        {
            Dialogs.Problem(this, "That screenshot couldn't be opened", "The file may have been moved, renamed or deleted.", ex.Message);
            RefreshRecents();
        }
    }

    internal static string SavePreviousScreenshot(BitmapSource image, string folder)
    {
        Directory.CreateDirectory(folder);
        string path = System.IO.Path.Combine(folder, "SnipCanvas-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) encoder.Save(file);
        return path;
    }
    private void SetImage(BitmapSource image)
    {
        sourceImage = image; imageEdits.Clear(); selectedText = -1;
        DisplayImage(image); UpdateTextSelection();
    }
    private void DisplayImage(BitmapSource image)
    {
        bitmap = image; photo.Source = image; photo.Width = canvas.Width = image.PixelWidth; photo.Height = canvas.Height = image.PixelHeight;
        if (stage.Children.Count != 1 || stage.Children[0] is not Viewbox)
        {
            stage.Children.Clear(); stage.Children.Add(new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = canvas });
        }
        SetHasImage(true);
        sizeLabel.Text = $"{image.PixelWidth} × {image.PixelHeight} px";
        RefreshChrome();
    }

    private void SelectTool(string name)
    {
        CancelDrag(); tool = name; selectedText = -1; UpdateTextSelection();
        if (toolButtons.TryGetValue(name, out var button)) button.IsChecked = true;
        Mouse.UpdateCursor();
    }

    /// <summary>Screenshots from high-resolution displays are shown scaled down, so marks scale up with the image to stay visible.</summary>
    private double ImageScale => bitmap == null ? 1 : Math.Clamp(Math.Max(bitmap.PixelWidth, bitmap.PixelHeight) / 1400.0, 1, 3);
    private double AnnotationThickness => Math.Round(ThicknessLevels[thicknessLevel] * ImageScale, 1);
    private double DefaultTextSize => Math.Round(26 * ImageScale / 2) * 2;
    /// <summary>Editor zoom, so on-screen guides keep a constant width whatever the screenshot size.</summary>
    private double Zoom => canvas.Width > 0 && stage.ActualWidth > 0 ? Math.Min(1, Math.Min(stage.ActualWidth / canvas.Width, stage.ActualHeight / canvas.Height)) : 1;

    private Point Position(MouseEventArgs e) { var p = e.GetPosition(canvas); return new Point(Math.Clamp(p.X, 0, canvas.Width), Math.Clamp(p.Y, 0, canvas.Height)); }
    private void Begin(object sender, MouseButtonEventArgs e)
    {
        if (bitmap == null) return;
        if (tool == "Text" && BeginTextSelection(Position(e), e.ClickCount)) return;
        start = Position(e);
        if (tool == "Text")
        {
            var point = start.Value; start = null;
            double size = DefaultTextSize;
            var textPreview = new TextBlock { FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.SemiBold, FontSize = size, IsHitTestVisible = false };
            Canvas.SetLeft(textPreview, point.X); Canvas.SetTop(textPreview, point.Y); canvas.Children.Add(textPreview);
            try
            {
                var dialog = new TextDialog(this, canvas.PointToScreen(point), annotationColor, (value, color) => {
                    textPreview.Text = value; textPreview.Foreground = new SolidColorBrush(color);
                });
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Value))
                {
                    annotationColor = dialog.TextColor;
                    CommitEdit(new TextEdit(point, dialog.Value, annotationColor, size));
                }
            }
            finally { canvas.Children.Remove(textPreview); }
            return;
        }
        canvas.CaptureMouse();
    }
    private void Move(object sender, MouseEventArgs e)
    {
        if (textDragStart != null) { if (e.LeftButton == MouseButtonState.Pressed) MoveSelectedText(Position(e)); return; }
        if (start is not Point p || bitmap == null) return;
        ClearPreview();
        var end = Position(e);
        double weight = AnnotationThickness, hairline = 2 / Math.Max(Zoom, 0.05);
        var ink = new SolidColorBrush(annotationColor);
        var rect = new Rect(p, end);
        switch (tool)
        {
            case "Arrow":
                preview = new System.Windows.Shapes.Path { Data = Edits.ArrowGeometry(p, end, weight), Stroke = ink, StrokeThickness = weight, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
                break;
            case "Box":
                preview = new Rectangle { Width = rect.Width, Height = rect.Height, Stroke = ink, StrokeThickness = weight, StrokeLineJoin = PenLineJoin.Round, RadiusX = Edits.BoxRadius(rect, weight), RadiusY = Edits.BoxRadius(rect, weight) };
                Canvas.SetLeft(preview, rect.X); Canvas.SetTop(preview, rect.Y);
                break;
            case "Crop":
                // Dim everything outside the area that will be kept.
                preview = new System.Windows.Shapes.Path {
                    Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, canvas.Width, canvas.Height)), new RectangleGeometry(rect)),
                    Fill = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), IsHitTestVisible = false };
                previewOutline = new Rectangle { Width = rect.Width, Height = rect.Height, Stroke = Brushes.White, StrokeThickness = hairline, StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false };
                Canvas.SetLeft(previewOutline, rect.X); Canvas.SetTop(previewOutline, rect.Y);
                break;
            default:
                var accent = Theme.Color("Accent");
                preview = new Rectangle { Width = rect.Width, Height = rect.Height, Stroke = Theme.Brush("Accent"), StrokeThickness = hairline, StrokeDashArray = new DoubleCollection { 4, 3 }, Fill = new SolidColorBrush(Color.FromArgb(56, accent.R, accent.G, accent.B)) };
                Canvas.SetLeft(preview, rect.X); Canvas.SetTop(preview, rect.Y);
                break;
        }
        canvas.Children.Add(preview);
        if (previewOutline != null) canvas.Children.Add(previewOutline);
    }
    private void End(object sender, MouseButtonEventArgs e)
    {
        if (textDragStart != null) { EndTextMove(); return; }
        if (start is not Point p || bitmap == null) return;
        var end = Position(e); CancelDrag();
        if ((end - p).Length < 3) return;
        var rect = new Rect(p, end);
        if (tool == "Arrow") CommitEdit(new ArrowEdit(p, end, annotationColor, AnnotationThickness));
        else if (tool == "Box") { if (rect.Width >= 3 && rect.Height >= 3) CommitEdit(new BoxEdit(rect, annotationColor, AnnotationThickness)); }
        else if (rect.Width >= 2 && rect.Height >= 2)
        {
            var r = new Int32Rect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
            CommitEdit(tool == "Crop" ? new CropEdit(r) : new BlurEdit(r));
        }
    }
    private void ClearPreview()
    {
        if (preview != null) { canvas.Children.Remove(preview); preview = null; }
        if (previewOutline != null) { canvas.Children.Remove(previewOutline); previewOutline = null; }
    }
    private void CancelDrag() { CancelTextMove(); start = null; ClearPreview(); if (canvas.IsMouseCaptured) canvas.ReleaseMouseCapture(); }
    private void Commit(BitmapSource next) { undo.Push(Snapshot()); redo.Clear(); SetImage(next); dirty = true; }
    private void Undo() { CancelDrag(); if (bitmap == null) { UndoDelete(); return; } if (undo.Count == 0) return; redo.Push(Snapshot()); Restore(undo.Pop()); dirty = true; }
    private void Redo() { CancelDrag(); if (bitmap == null || redo.Count == 0) return; undo.Push(Snapshot()); Restore(redo.Pop()); dirty = true; }
    private void Copy()
    {
        if (bitmap == null) return;
        try { Clipboard.SetImage(bitmap); Toast("Copied. Paste it anywhere with Ctrl+V."); }
        catch (Exception ex) { Dialogs.Problem(this, "The image couldn't be copied", "Another app may be using the clipboard. Click Copy to try again.", ex.Message); }
    }
    private void DeleteImage()
    {
        if (bitmap == null || busy) return;
        CancelDrag();
        deletedState = Snapshot(); selectedText = -1;
        deletedImage = bitmap; deletedImageWasDirty = dirty;
        bitmap = null; photo.Source = null; dirty = false;
        UpdateTextSelection();
        if (stage.Children.Count == 1 && stage.Children[0] is Viewbox viewbox) viewbox.Child = null;
        stage.Children.Clear();
        SetHasImage(false);
        status.Text = "";
    }

    private void UndoDelete()
    {
        if (deletedImage == null) return;
        if (deletedState != null) Restore(deletedState); else SetImage(deletedImage);
        dirty = deletedImageWasDirty; deletedState = null;
        deletedImage = null; RefreshUndoNotice();
    }

    private void OpenScreenshotFolder()
    {
        try
        {
            Directory.CreateDirectory(preferences.SaveFolder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(preferences.SaveFolder) { UseShellExecute = true });
        }
        catch (Exception ex) { Dialogs.Problem(this, "The folder couldn't be opened", "Check the screenshot folder in Settings.", ex.Message); }
    }

    private static void RevealFile(string path) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });

    private void Save()
    {
        if (bitmap == null) return;
        try
        {
            string path = lastSavedPath = SavePreviousScreenshot(bitmap, preferences.SaveFolder);
            dirty = false;
            Toast("Saved to " + System.IO.Path.GetFileName(preferences.SaveFolder.TrimEnd('\\', '/')), "check", "Success", "Show", () => { try { RevealFile(path); } catch (Exception) { } });
        }
        catch (Exception ex) { Dialogs.Problem(this, "The image couldn't be saved", "Choose a different screenshot folder in Settings, then try again.", ex.Message); }
    }
}

internal static class Edits
{
    private static BitmapSource Render(BitmapSource source, Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) { dc.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight)); draw(dc); }
        var result = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32); result.Render(visual); result.Freeze(); return result;
    }
    public static Geometry ArrowGeometry(Point a, Point b, double thickness = 4)
    {
        var direction = a - b; if (direction.Length < 1) return Geometry.Empty;
        direction.Normalize(); var side = new Vector(-direction.Y, direction.X);
        double head = thickness * 4.5, half = thickness * 2.25;
        var g = new StreamGeometry(); using (var c = g.Open()) { c.BeginFigure(a, false, false); c.LineTo(b, true, false); c.BeginFigure(b + direction * head + side * half, false, false); c.LineTo(b, true, false); c.LineTo(b + direction * head - side * half, true, false); } return g;
    }
    public static BitmapSource Arrow(BitmapSource image, Point a, Point b, Color? color = null, double thickness = 4) =>
        Render(image, dc => dc.DrawGeometry(null, new Pen(new SolidColorBrush(color ?? Colors.OrangeRed), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, ArrowGeometry(a, b, thickness)));
    public static double BoxRadius(Rect area, double thickness) => Math.Min(thickness * 1.2, Math.Min(area.Width, area.Height) / 2);
    public static BitmapSource Box(BitmapSource image, Rect area, Color color, double thickness) =>
        Render(image, dc => { double r = BoxRadius(area, thickness); dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(color), thickness) { LineJoin = PenLineJoin.Round }, area, r, r); });
    public static FormattedText FormatText(string text, Color color, double size) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), size, new SolidColorBrush(color), 1);
    public static BitmapSource Text(BitmapSource image, Point p, string text, Color? color = null, double size = 26) => Render(image, dc => {
        var formatted = FormatText(text, color ?? Colors.OrangeRed, size);
        dc.DrawText(formatted, p);
    });
    public static BitmapSource Blur(BitmapSource image, Int32Rect rect)
    {
        var source = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        int w = rect.Width, h = rect.Height, stride = w * 4;
        var pixels = new byte[h * stride]; source.CopyPixels(rect, pixels, stride, 0);
        var temp = new byte[pixels.Length]; var output = new byte[pixels.Length]; const int radius = 14;
        // Two separable box-blur passes. The selected region supplies all samples.
        for (int y = 0; y < h; y++) for (int c = 0; c < 4; c++)
        {
            int sum = 0; for (int x = 0; x <= Math.Min(radius, w-1); x++) sum += pixels[y*stride+x*4+c];
            for (int x = 0; x < w; x++) { int lo = Math.Max(0,x-radius), hi = Math.Min(w-1,x+radius); temp[y*stride+x*4+c] = (byte)(sum/(hi-lo+1)); if (x-radius >= 0) sum -= pixels[y*stride+(x-radius)*4+c]; if (x+radius+1 < w) sum += pixels[y*stride+(x+radius+1)*4+c]; }
        }
        for (int x = 0; x < w; x++) for (int c = 0; c < 4; c++)
        {
            int sum = 0; for (int y = 0; y <= Math.Min(radius,h-1); y++) sum += temp[y*stride+x*4+c];
            for (int y = 0; y < h; y++) { int lo = Math.Max(0,y-radius), hi = Math.Min(h-1,y+radius); output[y*stride+x*4+c] = (byte)(sum/(hi-lo+1)); if (y-radius >= 0) sum -= temp[(y-radius)*stride+x*4+c]; if (y+radius+1 < h) sum += temp[(y+radius+1)*stride+x*4+c]; }
        }
        var result = new WriteableBitmap(source); result.WritePixels(rect, output, stride, 0); result.Freeze(); return result;
    }
}
