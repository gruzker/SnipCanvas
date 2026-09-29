using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;

namespace SnipCanvas;

/// <summary>The right-click menu of the tray icon. It is a WinForms control, so it is drawn by hand to match the app theme.</summary>
internal sealed partial class MainWindow
{
    private double trayScale = 1;
    private int Px(int value) => (int)Math.Round(value * trayScale);

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        trayScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var menu = new Forms.ContextMenuStrip
        {
            ShowImageMargin = true, ShowCheckMargin = false, Font = new Font("Segoe UI", 9.5f),
            ImageScalingSize = new System.Drawing.Size(Px(16), Px(16)), Padding = new Forms.Padding(0, Px(5), 0, Px(5)),
            MinimumSize = new System.Drawing.Size(Px(244), 0)
        };
        void Add(string text, string icon, Action action, bool bold = false)
        {
            // Icon and label positions are drawn by TrayRenderer; the padding here only sets how tall each row is.
            var item = new Forms.ToolStripMenuItem(text) { Tag = icon, Padding = new Forms.Padding(Px(4), Px(6), Px(4), Px(6)), Margin = new Forms.Padding(0) };
            item.Click += (_, _) => action();
            if (bold) item.Font = new Font(menu.Font, System.Drawing.FontStyle.Bold);
            menu.Items.Add(item);
        }
        void Line() => menu.Items.Add(new Forms.ToolStripSeparator { Margin = new Forms.Padding(0, Px(3), 0, Px(3)) });
        Add("Open SnipCanvas", "@app", RestoreFromTray, bold: true);
        Add("Capture an area", "area", async () => await Take("Area"));
        Add("Capture a window", "window", async () => await Take("Window"));
        Add("Capture full screen", "screen", async () => await Take("Screen"));
        Line();
        Add("Open screenshot folder", "folder", OpenScreenshotFolder);
        Add("Settings", "settings", ShowSettings);
        Line();
        Add("Quit SnipCanvas", "power", Quit);
        menu.HandleCreated += (_, _) => Theme.RoundCorners(menu.Handle);
        return menu;
    }

    /// <summary>The tray icon needs the exact small frame from the icon file; scaling the 32 px frame down looks blurry.</summary>
    private static System.Drawing.Icon LoadTrayIcon()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/assets/snipcanvas.ico"))!.Stream;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        return new System.Drawing.Icon(copy, Forms.SystemInformation.SmallIconSize);
    }

    /// <summary>Colours and icons follow the app theme, so this runs at start-up and whenever the theme changes.</summary>
    private void ApplyTrayTheme()
    {
        if (trayMenu == null) return;
        trayMenu.Renderer = new TrayRenderer(trayScale);
        trayMenu.BackColor = ToDrawing(Theme.Color("Surface"));
        var ink = ToDrawing(Theme.Color("Ink"));
        trayMenu.ForeColor = ink;
        int size = Px(16);
        foreach (Forms.ToolStripItem item in trayMenu.Items)
        {
            item.ForeColor = ink;
            if (item.Tag is not string icon) continue;
            var old = item.Image;
            item.Image = icon == "@app" ? AppGlyph(size) : Glyph(icon, size);
            old?.Dispose();
        }
    }

    private static System.Drawing.Color ToDrawing(System.Windows.Media.Color color) => System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);

    private Bitmap AppGlyph(int size)
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/assets/snipcanvas.ico"))!.Stream;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        using var icon = new System.Drawing.Icon(copy, new System.Drawing.Size(size, size));
        return icon.ToBitmap();
    }

    /// <summary>Draws one of the app's line icons in the current text color at the menu's icon size.</summary>
    private Bitmap Glyph(string name, int size)
    {
        var path = new System.Windows.Shapes.Path
        {
            Data = Icons.Get(name), Stroke = new SolidColorBrush(Theme.Color("Ink")), StrokeThickness = 1.8,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
        };
        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(path);
        var box = new Viewbox { Width = size, Height = size, Child = canvas };
        box.Measure(new System.Windows.Size(size, size));
        box.Arrange(new Rect(0, 0, size, size));
        var rendered = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(box);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rendered));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        using var decoded = new Bitmap(stream);
        return new Bitmap(decoded);
    }

    private sealed class TrayColors : Forms.ProfessionalColorTable
    {
        private static System.Drawing.Color Of(string name) => ToDrawing(Theme.Color(name));
        public override System.Drawing.Color ToolStripDropDownBackground => Of("Surface");
        public override System.Drawing.Color ImageMarginGradientBegin => Of("Surface");
        public override System.Drawing.Color ImageMarginGradientMiddle => Of("Surface");
        public override System.Drawing.Color ImageMarginGradientEnd => Of("Surface");
        public override System.Drawing.Color MenuBorder => Of("LineStrong");
        public override System.Drawing.Color MenuItemBorder => Of("Hover");
        public override System.Drawing.Color MenuItemSelected => Of("Hover");
        public override System.Drawing.Color SeparatorDark => Of("Line");
        public override System.Drawing.Color SeparatorLight => Of("Line");
    }

    private sealed class TrayRenderer : Forms.ToolStripProfessionalRenderer
    {
        private readonly double scale;
        public TrayRenderer(double scale) : base(new TrayColors()) { RoundedEdges = false; this.scale = scale; }

        private static System.Drawing.Color Of(string name) => ToDrawing(Theme.Color(name));

        // The icon column shares the menu's own background instead of being shaded as a separate strip.
        protected override void OnRenderImageMargin(Forms.ToolStripRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int left = MenuX(e.Item, Dip(5)), right = MenuX(e.Item, e.Item.Owner!.Width - Dip(5));
            var bounds = new System.Drawing.Rectangle(left, Dip(1), right - left - 1, e.Item.Height - Dip(2) - 1);
            float radius = (float)(6 * scale), d = radius * 2;
            using var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90); path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90); path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            using var fill = new SolidBrush(Of("Pressed"));
            g.FillPath(fill, path);
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new System.Drawing.Pen(Of("Line"));
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, MenuX(e.Item, Dip(12)), y, MenuX(e.Item, e.Item.Owner!.Width - Dip(12)), y);
        }

        // Row layout, measured from the menu's own edges so both sides match:
        // [ 5 ][ highlight: 9 pad | 16 icon | 11 gap | label ... hint | 12 pad ][ 5 ]
        private int Dip(int value) => (int)Math.Round(value * scale);
        /// <summary>Converts an x position measured from the menu's left edge into the item's own coordinates.</summary>
        private static int MenuX(Forms.ToolStripItem item, int x) => x - item.Bounds.X;

        protected override void OnRenderItemImage(Forms.ToolStripItemImageRenderEventArgs e)
        {
            if (e.Image == null) return;
            int size = Dip(16);
            e.Graphics.DrawImage(e.Image, new System.Drawing.Rectangle(MenuX(e.Item, Dip(14)), (e.Item.Height - size) / 2, size, size));
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is Forms.ToolStripMenuItem item && !string.IsNullOrEmpty(item.ShortcutKeyDisplayString) && e.Text == item.ShortcutKeyDisplayString)
            {
                // The shortcut hint sits at the right edge in a quieter color.
                var size = Forms.TextRenderer.MeasureText(e.Graphics, e.Text, e.TextFont);
                e.TextColor = Of("Subtle");
                e.TextRectangle = new System.Drawing.Rectangle(MenuX(item, item.Owner!.Width - Dip(17)) - size.Width, 0, size.Width + 6, e.Item.Height);
            }
            else
            {
                int left = MenuX(e.Item, Dip(14 + 16 + 11));
                e.TextRectangle = new System.Drawing.Rectangle(left, 0, MenuX(e.Item, e.Item.Owner!.Width - Dip(17)) - left, e.Item.Height);
                e.TextColor = e.Item.Enabled ? Of("Ink") : Of("Subtle");
            }
            // Centre the text in the row, in line with the icon.
            e.TextFormat |= Forms.TextFormatFlags.VerticalCenter;
            base.OnRenderItemText(e);
        }
    }
}
