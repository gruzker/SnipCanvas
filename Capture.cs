using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace SnipCanvas;

internal static class Capture
{
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr arg);
    private delegate bool EnumProc(IntPtr handle, IntPtr arg);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr handle, int attr, out NativeRect rect, int size);
    [DllImport("dwmapi.dll", EntryPoint="DwmGetWindowAttribute")] private static extern int GetCloaked(IntPtr handle, int attr, out int value, int size);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }

    public static async Task<BitmapSource?> Take(string mode, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var bounds = SystemInformation.VirtualScreen;
        using var desktop = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(desktop))
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        Rectangle selection;
        if (mode == "Screen") selection = Screen.PrimaryScreen!.Bounds;
        else
        {
            var windows = new List<WindowTarget>();
            if (mode == "Window") EnumWindows((h, _) => {
                if (!IsWindowVisible(h) || IsIconic(h)) return true;
                if (GetCloaked(h, 14, out var cloaked, 4) == 0 && cloaked != 0) return true;
                if (DwmGetWindowAttribute(h, 9, out var r, 16) != 0) GetWindowRect(h, out r);
                var rect = Rectangle.Intersect(bounds, Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
                if (rect.Width > 30 && rect.Height > 30) windows.Add(new WindowTarget(h, rect));
                return true;
            }, IntPtr.Zero);
            using var overlay = new SelectionOverlay(desktop, bounds, windows, mode == "Window", cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (overlay.ShowDialog() != DialogResult.OK) return null;
            cancellation.ThrowIfCancellationRequested();
            if (mode == "Window")
            {
                var handle = overlay.SelectedWindow;
                // Wake browser video that may have paused while the selection overlay covered it.
                SetForegroundWindow(handle);
                return await GraphicsWindowCapture.TakeAsync(handle, cancellation);
            }
            selection = overlay.Selection;
        }
        cancellation.ThrowIfCancellationRequested();
        selection.Offset(-bounds.X, -bounds.Y);
        using var cropped = desktop.Clone(selection, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        return ToImage(cropped);
    }

    private static BitmapSource ToImage(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze();
        return image;
    }
}

internal sealed record WindowTarget(IntPtr Handle, Rectangle Bounds);

internal sealed class SelectionOverlay : Form
{
    private static readonly Color Accent = Color.FromArgb(108, 92, 246);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, System.Text.StringBuilder text, int max);

    private readonly Bitmap desktop;
    private readonly Rectangle screen;
    private readonly List<WindowTarget> windows;
    private readonly bool windowMode;
    private readonly Font hintFont = new("Segoe UI Semibold", 11.5f);
    private readonly Font badgeFont = new("Segoe UI Semibold", 10.5f);
    private Point? start;
    private Rectangle selected;
    private Rectangle hintBounds;
    private Rectangle hintScreen;
    private string hoverTitle = "";
    private readonly CancellationTokenRegistration cancellationRegistration;
    public Rectangle Selection { get; private set; }
    public IntPtr SelectedWindow { get; private set; }

    private float Dpi => DeviceDpi / 96f;

    public SelectionOverlay(Bitmap image, Rectangle bounds, List<WindowTarget> targets, bool selectWindow, CancellationToken cancellation = default)
    {
        desktop = image; screen = bounds; windows = targets; windowMode = selectWindow;
        AutoScaleMode = AutoScaleMode.None; FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual; Bounds = bounds;
        TopMost = true; ShowInTaskbar = false; DoubleBuffered = true; KeyPreview = true;
        Cursor = Cursors.Cross;
        Shown += (_, _) => {
            if (cancellation.IsCancellationRequested) { CancelSelection(); return; }
            Activate(); Focus();
        };
        cancellationRegistration = cancellation.Register(() => {
            if (!IsHandleCreated || IsDisposed) return;
            try { BeginInvoke(new Action(() => { if (!IsDisposed) CancelSelection(); })); }
            catch (InvalidOperationException) { /* The overlay has already closed. */ }
        });
        MouseDown += (_, e) => {
            if (e.Button == MouseButtons.Right) { CancelSelection(); return; }
            if (e.Button == MouseButtons.Left) { start = e.Location; Capture = true; Invalidate(hintBounds); }
        };
        MouseMove += (_, e) => {
            var before = selected;
            if (windowMode) SelectWindowAt(e.Location);
            else if (start is Point s) selected = Rectangle.FromLTRB(Math.Min(s.X,e.X),Math.Min(s.Y,e.Y),Math.Max(s.X,e.X),Math.Max(s.Y,e.Y));
            selected = Rectangle.Intersect(ClientRectangle, selected);
            MoveHint(e.Location);
            if (before != selected) Invalidate(Rectangle.Union(Extent(before), Extent(selected)));
        };
        MouseUp += (_, e) => {
            Capture = false;
            if (windowMode) SelectWindowAt(e.Location);
            if (e.Button != MouseButtons.Left || selected.Width < 2 || selected.Height < 2) return;
            Selection = selected; var r = Selection; r.Offset(screen.X,screen.Y); Selection = r;
            DialogResult = DialogResult.OK; Close();
        };
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if ((keyData & Keys.KeyCode) == Keys.Escape) { CancelSelection(); return true; }
        return base.ProcessCmdKey(ref message, keyData);
    }

    private void CancelSelection()
    {
        start = null; selected = Rectangle.Empty; Selection = Rectangle.Empty;
        SelectedWindow = IntPtr.Zero; Capture = false;
        DialogResult = DialogResult.Cancel; Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { cancellationRegistration.Dispose(); hintFont.Dispose(); badgeFont.Dispose(); }
        base.Dispose(disposing);
    }

    private void SelectWindowAt(Point location)
    {
        var previous = SelectedWindow;
        selected = Rectangle.Empty; SelectedWindow = IntPtr.Zero;
        var point = new Point(location.X + screen.X, location.Y + screen.Y);
        foreach (var target in windows)
        {
            if (!target.Bounds.Contains(point)) continue;
            SelectedWindow = target.Handle; selected = target.Bounds;
            selected.Offset(-screen.X, -screen.Y); break;
        }
        if (SelectedWindow == previous) return;
        hoverTitle = "";
        if (SelectedWindow == IntPtr.Zero) return;
        var text = new System.Text.StringBuilder(256);
        if (GetWindowText(SelectedWindow, text, text.Capacity) > 0) hoverTitle = text.Length > 42 ? text.ToString(0, 41) + "…" : text.ToString();
    }

    /// <summary>Everything painted for a selection, so a move only repaints what changed.</summary>
    private Rectangle Extent(Rectangle area)
    {
        if (area.IsEmpty) return Rectangle.Empty;
        int band = (int)(56 * Dpi), width = (int)(560 * Dpi), edge = (int)(6 * Dpi);
        var frame = Rectangle.Inflate(area, edge, edge);
        var badge = Rectangle.FromLTRB(area.Left - edge, area.Top - band, area.Left + Math.Max(area.Width, width), area.Bottom + band);
        return Rectangle.Union(frame, badge);
    }

    private void MoveHint(Point client)
    {
        var current = Screen.FromPoint(new Point(client.X + screen.X, client.Y + screen.Y)).Bounds;
        if (current == hintScreen) return;
        hintScreen = current;
        Invalidate();
    }

    private static GraphicsPath Rounded(Rectangle r, float radius)
    {
        float d = radius * 2; var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }

    private const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoClipping;

    /// <summary>Draws one line of text vertically centred on <paramref name="centerY"/>; the box is generous so glyphs are never cut off.</summary>
    private static void Line(Graphics g, string text, Font font, int x, int centerY, Size size, Color color) =>
        TextRenderer.DrawText(g, text, font, new Rectangle(x, centerY - size.Height, size.Width + 2, size.Height * 2), color, Flags);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; var clip = e.ClipRectangle;
        g.DrawImage(desktop, clip, clip, GraphicsUnit.Pixel);
        using (var shade = new SolidBrush(Color.FromArgb(125, 8, 10, 16)))
        {
            if (selected.IsEmpty) g.FillRectangle(shade, clip);
            else
            {
                var size = ClientSize;
                foreach (var band in new[] {
                    new Rectangle(0, 0, size.Width, selected.Top), new Rectangle(0, selected.Bottom, size.Width, size.Height - selected.Bottom),
                    new Rectangle(0, selected.Top, selected.Left, selected.Height), new Rectangle(selected.Right, selected.Top, size.Width - selected.Right, selected.Height) })
                {
                    var part = Rectangle.Intersect(band, clip);
                    if (!part.IsEmpty) g.FillRectangle(shade, part);
                }
            }
        }
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (!selected.IsEmpty) DrawSelection(g);
        if (start == null) DrawHint(g);
    }

    private void DrawSelection(Graphics g)
    {
        float k = Dpi;
        if (windowMode) { using var tint = new SolidBrush(Color.FromArgb(52, Accent)); g.FillRectangle(tint, selected); }
        using (var pen = new Pen(Accent, 2f * k) { LineJoin = LineJoin.Miter })
        {
            float half = k;
            g.DrawRectangle(pen, selected.X - half, selected.Y - half, selected.Width - 1 + 2 * half, selected.Height - 1 + 2 * half);
        }
        using (var hairline = new Pen(Color.FromArgb(190, 255, 255, 255), 1f)) g.DrawRectangle(hairline, selected.X, selected.Y, selected.Width - 1, selected.Height - 1);

        string label = windowMode && hoverTitle.Length > 0 ? $"{hoverTitle}   {selected.Width} × {selected.Height}" : $"{selected.Width} × {selected.Height}";
        var text = TextRenderer.MeasureText(g, label, badgeFont, Size.Empty, Flags);
        int padX = (int)(10 * k), padY = (int)(6 * k), gap = (int)(10 * k);
        var pill = new Rectangle(0, 0, text.Width + padX * 2, text.Height + padY * 2);
        pill.X = Math.Clamp(selected.Left, 4, Math.Max(4, ClientSize.Width - pill.Width - 4));
        pill.Y = selected.Bottom + gap;
        if (pill.Bottom > ClientSize.Height - 4) pill.Y = selected.Top - gap - pill.Height;
        if (pill.Y < 4) pill.Y = Math.Max(4, selected.Bottom - gap - pill.Height);
        using (var path = Rounded(pill, pill.Height / 2f))
        using (var fill = new SolidBrush(Color.FromArgb(232, 20, 22, 31)))
        using (var edge = new Pen(Color.FromArgb(60, 255, 255, 255), 1f))
        { g.FillPath(fill, path); g.DrawPath(edge, path); }
        Line(g, label, badgeFont, pill.X + padX, pill.Y + pill.Height / 2, text, Color.White);
    }

    private void DrawHint(Graphics g)
    {
        float k = Dpi;
        var area = hintScreen.IsEmpty ? Screen.PrimaryScreen!.Bounds : hintScreen;
        area.Offset(-screen.X, -screen.Y);
        string lead = windowMode ? "Click a window to capture it" : "Drag to select an area";
        var leadSize = TextRenderer.MeasureText(g, lead, hintFont, Size.Empty, Flags);
        var capSize = TextRenderer.MeasureText(g, "Esc", hintFont, Size.Empty, Flags);
        var tailSize = TextRenderer.MeasureText(g, "to cancel", hintFont, Size.Empty, Flags);
        int pad = (int)(18 * k), gap = (int)(14 * k), tight = (int)(8 * k), capPad = (int)(7 * k), capV = (int)(3 * k);
        int content = leadSize.Width + gap + capSize.Width + capPad * 2 + tight + tailSize.Width;
        var pill = new Rectangle(area.X + (area.Width - content - pad * 2) / 2, area.Y + (int)(28 * k), content + pad * 2, leadSize.Height + (int)(22 * k));
        hintBounds = Rectangle.Inflate(pill, 3, 3);
        using (var path = Rounded(pill, pill.Height / 2f))
        using (var fill = new SolidBrush(Color.FromArgb(232, 20, 22, 31)))
        using (var edge = new Pen(Color.FromArgb(60, 255, 255, 255), 1f))
        { g.FillPath(fill, path); g.DrawPath(edge, path); }
        int x = pill.X + pad, cy = pill.Y + pill.Height / 2;
        Line(g, lead, hintFont, x, cy, leadSize, Color.White);
        x += leadSize.Width + gap;
        var cap = new Rectangle(x, cy - capSize.Height / 2 - capV, capSize.Width + capPad * 2, capSize.Height + capV * 2);
        using (var capPath = Rounded(cap, 5 * k))
        using (var capEdge = new Pen(Color.FromArgb(120, 255, 255, 255), 1f))
        using (var capFill = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
        { g.FillPath(capFill, capPath); g.DrawPath(capEdge, capPath); }
        Line(g, "Esc", hintFont, cap.X + capPad, cy, capSize, Color.White);
        x = cap.Right + tight;
        Line(g, "to cancel", hintFont, x, cy, tailSize, Color.FromArgb(200, 205, 220));
    }
}
