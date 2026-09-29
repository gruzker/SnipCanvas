using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnipCanvas;

/// <summary>Modal windows that follow the app theme, replacing the stock Windows message box.</summary>
internal static class Dialogs
{
    private static ImageSource? appIcon;
    internal static ImageSource AppIcon => appIcon ??= BitmapFrame.Create(new Uri("pack://application:,,,/assets/snipcanvas.ico"));

    internal static Window Create(Window? owner, string title, double width)
    {
        bool ownerShown = owner is { IsVisible: true };
        var dialog = new Window
        {
            Title = title, Width = width, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = !ownerShown, UseLayoutRounding = true, FontFamily = Theme.Font, FontSize = 13, Icon = AppIcon,
            WindowStartupLocation = ownerShown ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
        };
        if (ownerShown) dialog.Owner = owner; else dialog.Topmost = true;
        dialog.SetResourceReference(Control.BackgroundProperty, "Surface");
        dialog.SetResourceReference(Control.ForegroundProperty, "Ink");
        dialog.SourceInitialized += (_, _) => Theme.StyleTitleBar(dialog);
        return dialog;
    }

    /// <summary>Tells the user something went wrong, in plain language, with the technical reason underneath.</summary>
    internal static void Problem(Window? owner, string title, string message, string? details = null)
    {
        var dialog = Create(owner, "SnipCanvas", 460);
        var layout = new Grid { Margin = new Thickness(24) };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition());

        var tile = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(13), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 16, 0), Child = Ui.Icon("alert", 22, "Warning") };
        tile.SetResourceReference(Border.BackgroundProperty, "WarningSoft");
        layout.Children.Add(tile);

        var text = new StackPanel();
        text.Children.Add(Ui.Text(title, 17, "Ink", FontWeights.SemiBold, wrap: true));
        var body = Ui.Text(message, 13, "Muted", wrap: true); body.Margin = new Thickness(0, 8, 0, 0); text.Children.Add(body);
        if (!string.IsNullOrWhiteSpace(details))
        {
            var reason = Ui.Text(details, 12, "Subtle", wrap: true); reason.Margin = new Thickness(0, 12, 0, 0); text.Children.Add(reason);
        }
        var ok = Ui.Styled("PrimaryButton", "OK", () => dialog.DialogResult = true);
        ok.IsDefault = ok.IsCancel = true; ok.MinWidth = 88; ok.HorizontalAlignment = HorizontalAlignment.Right; ok.Margin = new Thickness(0, 20, 0, 0);
        text.Children.Add(ok);
        Grid.SetColumn(text, 1); layout.Children.Add(text);
        dialog.Content = layout;
        dialog.ShowDialog();
    }
}
