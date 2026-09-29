using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace SnipCanvas;

internal sealed class TextDialog : Window
{
    private readonly TextBox input = new() { FontSize = 15, Padding = new Thickness(2, 1, 2, 1), AcceptsReturn = true, MinHeight = 44, MaxHeight = 96, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBox hex = new() { Width = 86, MaxLength = 7, Padding = new Thickness(6, 1, 6, 1), FontSize = 12.5, VerticalContentAlignment = VerticalAlignment.Center, MinHeight = 28 };
    private readonly Button add = new() { Content = "Add text", MinWidth = 88, MinHeight = 34, IsEnabled = false, IsDefault = false };
    private readonly List<(RadioButton Button, Color Color)> swatches = new();
    public string Value => input.Text;
    public Color TextColor { get; private set; }

    public TextDialog(Window owner, Point screenAnchor, Color initialColor, Action<string, Color> preview, string initialText = "")
    {
        Owner = owner; Icon = owner.Icon; ShowInTaskbar = false;
        Title = initialText.Length > 0 ? "Edit text" : "Add text"; Width = 354; SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true; Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.Manual; UseLayoutRounding = true;
        FontFamily = Theme.Font; SetResourceReference(ForegroundProperty, "Ink");
        var panel = new StackPanel();
        var shell = new Border { CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), Padding = new Thickness(16), Margin = new Thickness(7), Child = panel,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = .3 } };
        shell.SetResourceReference(Border.BackgroundProperty, "Surface"); shell.SetResourceReference(Border.BorderBrushProperty, "LineStrong"); Content = shell;

        var header = new DockPanel { Margin = new Thickness(2, 0, 2, 10) };
        var hint = Ui.Text("Shift+Enter for a new line", 11.5, "Subtle"); DockPanel.SetDock(hint, Dock.Right); header.Children.Add(hint);
        header.Children.Add(Ui.Text(initialText.Length > 0 ? "Edit text" : "Add text", 14.5, "Ink", FontWeights.SemiBold)); panel.Children.Add(header);

        System.Windows.Automation.AutomationProperties.SetName(input, "Screenshot text"); input.ToolTip = "Type text to preview it on your screenshot";
        var inputFrame = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Padding = new Thickness(8, 6, 8, 6), Child = input };
        inputFrame.SetResourceReference(Border.BackgroundProperty, "Field"); inputFrame.SetResourceReference(Border.BorderBrushProperty, "Line");
        // The frame provides the chrome so the text area itself stays borderless.
        input.BorderThickness = new Thickness(0); input.Background = Brushes.Transparent; input.Padding = new Thickness(0);
        input.GotKeyboardFocus += (_, _) => inputFrame.SetResourceReference(Border.BorderBrushProperty, "Accent");
        input.LostKeyboardFocus += (_, _) => inputFrame.SetResourceReference(Border.BorderBrushProperty, "Line");
        panel.Children.Add(inputFrame);

        var colors = new DockPanel { Margin = new Thickness(0, 12, 0, 12), LastChildFill = false };
        hex.ToolTip = "Custom text color, for example #FF4500";
        System.Windows.Automation.AutomationProperties.SetName(hex, "Text color hex code"); DockPanel.SetDock(hex, Dock.Right); colors.Children.Add(hex);
        foreach (var (name, color) in new[] { ("White", Colors.White), ("Black", Colors.Black), ("Red", Colors.OrangeRed), ("Yellow", Colors.Gold), ("Green", Colors.LimeGreen), ("Blue", Colors.DodgerBlue), ("Purple", Colors.MediumPurple) })
        {
            var swatch = new RadioButton { GroupName = "textColor", Background = new SolidColorBrush(color), ToolTip = name };
            swatch.SetResourceReference(FrameworkElement.StyleProperty, "SwatchButton");
            System.Windows.Automation.AutomationProperties.SetName(swatch, name + " text");
            var chosen = color; swatch.Click += (_, _) => hex.Text = Hex(chosen);
            swatches.Add((swatch, color)); colors.Children.Add(swatch);
        }
        panel.Children.Add(colors);

        var actions = new DockPanel();
        add.SetResourceReference(StyleProperty, "PrimaryButton"); add.MinHeight = 34;
        DockPanel.SetDock(add, Dock.Right); actions.Children.Add(add);
        var cancel = Ui.Styled("GhostButton", "Cancel"); cancel.MinHeight = 34; cancel.HorizontalAlignment = HorizontalAlignment.Left;
        cancel.Click += (_, _) => DialogResult = false; actions.Children.Add(cancel); panel.Children.Add(actions);
        add.Click += (_, _) => DialogResult = true;
        void Update()
        {
            bool valid = hex.Text.Length == 7 && hex.Text[0] == '#' && uint.TryParse(hex.Text.AsSpan(1), System.Globalization.NumberStyles.AllowHexSpecifier, null, out _);
            add.IsEnabled = valid && !string.IsNullOrWhiteSpace(input.Text);
            if (!valid) { hex.BorderBrush = Theme.Brush("Danger"); return; }
            hex.SetResourceReference(Control.BorderBrushProperty, "Line");
            TextColor = (Color)ColorConverter.ConvertFromString(hex.Text);
            foreach (var swatch in swatches) swatch.Button.IsChecked = swatch.Color == TextColor;
            preview(Value, TextColor);
        }
        input.TextChanged += (_, _) => Update(); hex.TextChanged += (_, _) => Update(); input.Text = initialText; hex.Text = Hex(initialColor);
        if (initialText.Length > 0) add.Content = "Apply";
        Loaded += (_, _) => { PlaceNear(screenAnchor); input.Focus(); };
        PreviewKeyDown += (_, e) => {
            if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
            else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { if (add.IsEnabled) DialogResult = true; e.Handled = true; }
        };
    }

    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private void PlaceNear(Point screenAnchor)
    {
        var transform = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).CompositionTarget.TransformFromDevice;
        var anchor = transform.Transform(screenAnchor);
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)screenAnchor.X, (int)screenAnchor.Y)).WorkingArea;
        var min = transform.Transform(new Point(screen.Left, screen.Top)); var max = transform.Transform(new Point(screen.Right, screen.Bottom));
        Left = Math.Clamp(anchor.X - 10, min.X, Math.Max(min.X, max.X - ActualWidth));
        double above = anchor.Y - ActualHeight - 8;
        Top = Math.Clamp(above >= min.Y ? above : anchor.Y + 30, min.Y, Math.Max(min.Y, max.Y - ActualHeight));
    }
}
