using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SnipCanvas;

internal sealed partial class MainWindow
{
    private CapturePreview? capturePreview;
    private string? lastSavedPath;

    private void DismissCapturePreview()
    {
        capturePreview?.Close();
        capturePreview = null;
    }

    private void ShowCapturePreview(BitmapSource image, bool copied)
    {
        DismissCapturePreview();
        Func<bool> copyAgain = () => {
            try { Clipboard.SetImage(image); return true; }
            catch (System.Runtime.InteropServices.ExternalException) { return false; }
        };
        string? path = lastSavedPath;
        var preview = new CapturePreview(image, () => { ShowEditor(); RestoreFromTray(); },
            copied ? "Copied and saved" : "Screenshot saved", copied ? null : copyAgain,
            path == null ? null : () => { try { RevealFile(path); } catch (Exception) { } });
        capturePreview = preview;
        preview.Closed += (_, _) => { if (capturePreview == preview) capturePreview = null; };
        preview.Show();
    }
}

internal sealed class CapturePreview : Window
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(3);
    private readonly DispatcherTimer timer = new() { Interval = Lifetime };

    internal CapturePreview(BitmapSource image, Action openEditor, string message = "Screenshot saved", Func<bool>? copy = null, Action? showFolder = null)
    {
        NameScope.SetNameScope(this, new NameScope());
        Title = "Screenshot saved";
        Width = 340; Height = 322;
        UseLayoutRounding = true; FontFamily = Theme.Font; FontSize = 13;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        AllowsTransparency = true; Background = Brushes.Transparent;
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 16; Top = area.Bottom - Height - 16;

        var panel = new StackPanel();
        var header = new DockPanel { Height = 34, Margin = new Thickness(2, 0, 0, 8) };
        var close = Ui.Styled("IconButton", Ui.Icon("close", 15, "Muted"), () => Close(), "Dismiss preview", "Dismiss preview");
        close.Width = close.Height = 30; close.MinHeight = 30;
        RegisterName("DismissPreview", close);
        DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        var saved = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var check = Ui.Icon("check", 17, "Success", 2.2); check.Margin = new Thickness(0, 0, 8, 0); saved.Children.Add(check);
        var title = Ui.Text(message, 14, "Ink", FontWeights.SemiBold); saved.Children.Add(title);
        header.Children.Add(saved); panel.Children.Add(header);

        var thumbnail = new Image { Source = image, Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapScalingMode(thumbnail, BitmapScalingMode.HighQuality);
        RegisterName("ScreenshotThumbnail", thumbnail);
        var imageFrame = new Border { Child = thumbnail, CornerRadius = new CornerRadius(9), Height = 160 };
        imageFrame.SetResourceReference(Border.BackgroundProperty, "Canvas");
        imageFrame.SizeChanged += (_, _) => imageFrame.Clip = new RectangleGeometry(
            new Rect(0, 0, imageFrame.ActualWidth, imageFrame.ActualHeight), 9, 9);
        void OpenEditor() { Close(); openEditor(); }
        var thumbnailButton = Ui.Styled("ThumbButton", imageFrame, OpenEditor, "Open in the editor", "Open screenshot");
        RegisterName("OpenThumbnail", thumbnailButton);
        panel.Children.Add(thumbnailButton);

        var actions = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        actions.ColumnDefinitions.Add(new ColumnDefinition());
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var open = Ui.Styled("PrimaryButton", Ui.IconLabel("edit", "Edit", out _, 16), OpenEditor, "Open in the editor to mark it up", "Open screenshot in SnipCanvas");
        open.MinHeight = 36;
        RegisterName("OpenScreenshot", open);
        actions.Children.Add(open);
        if (copy != null)
        {
            Button? copyButton = null;
            copyButton = Ui.Styled("Button", Ui.IconLabel("copy", "Copy", out _, 16), () => {
                if (!copy()) return;
                title.Text = "Copied and saved"; copyButton!.Visibility = Visibility.Collapsed;
            }, "Copy to clipboard", "Copy screenshot");
            copyButton.Margin = new Thickness(8, 0, 0, 0); copyButton.MinHeight = 36;
            Grid.SetColumn(copyButton, 1); actions.Children.Add(copyButton);
        }
        if (showFolder != null)
        {
            var folder = Ui.Styled("Button", Ui.Icon("folder", 17), showFolder, "Show in folder", "Show in folder");
            folder.Margin = new Thickness(8, 0, 0, 0); folder.MinHeight = 36; folder.Width = 40; folder.Padding = new Thickness(0);
            Grid.SetColumn(folder, 2); actions.Children.Add(folder);
        }
        panel.Children.Add(actions);

        // Time left before the preview leaves; hovering pauses it so there is never a rush.
        var scale = new ScaleTransform(1, 1);
        var remaining = new Border { Height = 3, CornerRadius = new CornerRadius(1.5), RenderTransform = scale, RenderTransformOrigin = new Point(0, 0.5) };
        remaining.SetResourceReference(Border.BackgroundProperty, "Accent");
        var track = new Border { Height = 3, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0, 12, 0, 0), Child = remaining };
        track.SetResourceReference(Border.BackgroundProperty, "Line");
        panel.Children.Add(track);

        bool animate = SystemParameters.ClientAreaAnimation;
        var slide = new TranslateTransform(0, animate ? -24 : 0);
        var card = new Border { Child = panel, Margin = new Thickness(12), Padding = new Thickness(12),
            RenderTransform = slide, Opacity = animate ? 0 : 1,
            CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), SnapsToDevicePixels = true,
            Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 3, Opacity = 0.32, Color = Colors.Black } };
        card.SetResourceReference(Border.BorderBrushProperty, "Line");
        card.SetResourceReference(Border.BackgroundProperty, "Surface");
        Content = card;

        void Countdown()
        {
            timer.Stop(); timer.Start();
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            if (animate) scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 0, Lifetime));
            else scale.ScaleX = 0;
        }
        card.MouseEnter += (_, _) => { timer.Stop(); scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scale.ScaleX = 1; };
        card.MouseLeave += (_, _) => Countdown();

        // Displaying or touching the preview must not steal keyboard focus.
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)!.AddHook(
            (IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) => {
                if (message == 0x0021) { handled = true; return new IntPtr(3); } // WM_MOUSEACTIVATE / MA_NOACTIVATE
                return IntPtr.Zero;
            });
        timer.Tick += (_, _) => Close();
        ContentRendered += (_, _) => {
            if (animate)
            {
                var duration = TimeSpan.FromMilliseconds(220);
                slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-24, 0, duration) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration));
            }
            Countdown();
        };
        Closed += (_, _) => timer.Stop();
    }
}
