using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SnipCanvas;

internal sealed partial class MainWindow
{
    private static readonly string ThemeFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnipCanvas", "theme.txt");
    private static readonly (string Name, Color Color)[] Swatches = {
        ("Red", Colors.OrangeRed), ("Yellow", Colors.Gold), ("Green", Colors.LimeGreen), ("Blue", Colors.DodgerBlue),
        ("Purple", Colors.MediumPurple), ("White", Colors.White), ("Black", Colors.Black) };
    private static readonly double[] ThicknessLevels = { 3, 5, 8 };

    private string themeMode = "system";
    private FrameworkElement editorPage = null!;
    private Border commandBar = null!;
    private FrameworkElement newShotSplit = null!;
    private FrameworkElement homeView = null!;
    private FrameworkElement editView = null!;
    private StackPanel noticeHost = null!;
    private Border shortcutNotice = null!;
    private TextBlock shortcutNoticeText = null!;
    private Border undoNotice = null!;
    private Button undoButton = null!;
    private Button redoButton = null!;
    private StackPanel saveState = null!;
    private Ellipse saveDot = null!;
    private TextBlock saveText = null!;
    private TextBlock sizeLabel = null!;
    private Button statusFolder = null!;
    private TextBlock toolHint = null!;
    private StackPanel colorOptions = null!;
    private FrameworkElement thicknessOptions = null!;
    private readonly List<(RadioButton Button, Color Color)> swatchButtons = new();
    private readonly List<RadioButton> thicknessButtons = new();
    private readonly List<FrameworkElement> compactLabels = new();
    private StackPanel homeHint = null!;
    private UniformGrid recentGrid = null!;
    private FrameworkElement recentSection = null!;
    private FrameworkElement recentEmpty = null!;
    private int recentVersion;
    private MenuItem areaMenuItem = null!;
    private ContextMenu captureMenu = null!;
    private Border toast = null!;
    private ContentControl toastIcon = null!;
    private TextBlock toastText = null!;
    private Button toastAction = null!;
    private Action? toastActionHandler;
    private DispatcherTimer? toastTimer;
    private int thicknessLevel = 1;
    private bool startupEnabled;
    private bool startupReadable = true;

    private void BuildInterface()
    {
        Theme.Install();
        Title = "SnipCanvas"; Width = 1080; MinWidth = 860; MinHeight = 560;
        Height = Math.Min(760, SystemParameters.WorkArea.Height * 0.92);
        Icon = Dialogs.AppIcon;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; FontFamily = Theme.Font; FontSize = 13; UseLayoutRounding = true;
        themeMode = ReadThemeMode();
        Theme.Apply(ResolveDark());
        Resources.MergedDictionaries.Add(Theme.Palette);
        SetResourceReference(BackgroundProperty, "Page"); SetResourceReference(ForegroundProperty, "Ink");
        try { startupEnabled = StartupSetting.IsEnabled(); }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException) { startupReadable = false; }

        var root = new Grid();
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
            root.RowDefinitions.Add(new RowDefinition { Height = height });
        Content = root; editorPage = root;

        var appBar = BuildAppBar(); Grid.SetRow(appBar, 0); root.Children.Add(appBar);
        commandBar = BuildCommandBar(); Grid.SetRow(commandBar, 1); root.Children.Add(commandBar);
        noticeHost = BuildNotices(); Grid.SetRow(noticeHost, 2); root.Children.Add(noticeHost);

        var body = new Grid();
        homeView = BuildHome(); editView = BuildEditView(); toast = BuildToast();
        body.Children.Add(homeView); body.Children.Add(editView); body.Children.Add(toast);
        Grid.SetRow(body, 3); root.Children.Add(body);

        var statusBar = BuildStatusBar(); Grid.SetRow(statusBar, 4); root.Children.Add(statusBar);
        SelectTool("Arrow");
        SetHasImage(false);
        SizeChanged += (_, e) => ApplyCompact(e.NewSize.Width < 1000);
    }

    // ---------------------------------------------------------------- app bar

    private FrameworkElement BuildAppBar()
    {
        var bar = new DockPanel { Margin = new Thickness(20, 0, 14, 0), Height = 56 };
        var settings = Ui.Styled("IconButton", Ui.Icon("settings", 19), ShowSettings, "Settings  (Ctrl+,)", "Settings");
        DockPanel.SetDock(settings, Dock.Right); bar.Children.Add(settings);

        newShotSplit = BuildNewScreenshotButton();
        newShotSplit.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(newShotSplit, Dock.Right); bar.Children.Add(newShotSplit);

        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var logoSource = new BitmapImage();
        logoSource.BeginInit(); logoSource.UriSource = new Uri("pack://application:,,,/assets/snipcanvas.png"); logoSource.DecodePixelWidth = 96; logoSource.EndInit(); logoSource.Freeze();
        var logo = new Image { Width = 28, Height = 28, Margin = new Thickness(0, 0, 10, 0), Source = logoSource, Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
        brand.Children.Add(logo);
        brand.Children.Add(Ui.Text("SnipCanvas", 16, "Ink", FontWeights.SemiBold));
        bar.Children.Add(brand);
        return bar;
    }

    private FrameworkElement BuildNewScreenshotButton()
    {
        var menu = captureMenu = new ContextMenu();
        areaMenuItem = CaptureItem(menu, "Area", "area", "Capture an area", "Drag to choose what to capture", preferences.Shortcut.Label);
        CaptureItem(menu, "Window", "window", "Capture a window", "Click a window; anything covering it is left out");
        CaptureItem(menu, "Screen", "screen", "Capture full screen", "Your entire main screen");

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var main = Ui.Styled("SplitLeftButton", Ui.IconLabel("plus", "New screenshot", out _), async () => await Take("Area"), "Capture an area", "New screenshot");
        main.Padding = new Thickness(14, 0, 12, 0);
        var seam = new Border { Width = 1, Background = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)) };
        var more = Ui.Styled("SplitRightButton", Ui.Icon("chevron-down", 16), null, "Choose what to capture", "Choose what to capture");
        more.Padding = new Thickness(8, 0, 8, 0);
        more.Click += (_, _) => OpenMenuUnder(menu, more);
        row.Children.Add(main); row.Children.Add(seam); row.Children.Add(more);
        return row;
    }

    /// <summary>Opens a menu under its button with the right edges lined up, so it never spills past the window.</summary>
    internal static void OpenMenuUnder(ContextMenu menu, FrameworkElement target)
    {
        menu.PlacementTarget = target;
        menu.Placement = PlacementMode.Custom;
        // The menu template keeps an 8 px margin for its shadow; compensate so the visible edges align.
        menu.CustomPopupPlacementCallback = (popup, anchor, _) => new[] { new CustomPopupPlacement(new Point(anchor.Width - popup.Width + 8, anchor.Height - 4), PopupPrimaryAxis.None) };
        menu.IsOpen = true;
    }

    private MenuItem CaptureItem(ContextMenu menu, string mode, string icon, string header, string tip, string? gesture = null)
    {
        var item = new MenuItem { Header = header, ToolTip = tip, Icon = MenuTile(icon), InputGestureText = gesture ?? "" };
        item.Click += async (_, _) => await Take(mode);
        menu.Items.Add(item);
        return item;
    }

    /// <summary>Rounded square that holds a menu icon; brand colours are used for share targets.</summary>
    internal static Border MenuTile(string icon, string? colorHex = null)
    {
        var tile = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(8) };
        if (colorHex == null)
        {
            tile.SetResourceReference(Border.BackgroundProperty, "Selected");
            tile.Child = Ui.Icon(icon, 15, "AccentText");
        }
        else
        {
            tile.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            tile.Child = Ui.Icon(icon, 15, null, 1.75);
            System.Windows.Documents.TextElement.SetForeground(tile, Brushes.White);
        }
        return tile;
    }

    // ---------------------------------------------------------------- command bar (tools + actions)

    private Border BuildCommandBar()
    {
        var bar = new Grid { Margin = new Thickness(16, 0, 16, 0), Height = 52 };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition());
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        foreach (var (name, icon, label, key, tip) in new[] {
            ("Arrow", "arrow", "Arrow", "A", "Point at something"),
            ("Box", "box", "Box", "R", "Draw a box around something"),
            ("Text", "text", "Text", "T", "Add or edit text"),
            ("Blur", "blur", "Blur", "B", "Hide private details"),
            ("Crop", "crop", "Crop", "C", "Keep only part of the image") })
        {
            var button = new RadioButton { GroupName = "tools", Content = Ui.IconLabel(icon, label, out var text), ToolTip = $"{tip}  ({key})" };
            button.SetResourceReference(FrameworkElement.StyleProperty, "ToolButton");
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.Checked += (_, _) => { if (tool != name) SelectTool(name); };
            compactLabels.Add(text);
            toolButtons[name] = button; toolbar.Children.Add(button);
        }
        toolbar.Children.Add(Ui.Divider());
        undoButton = Ui.Styled("IconButton", Ui.Icon("undo", 18), Undo, "Undo  (Ctrl+Z)", "Undo");
        redoButton = Ui.Styled("IconButton", Ui.Icon("redo", 18), Redo, "Redo  (Ctrl+Y)", "Redo");
        toolbar.Children.Add(undoButton); toolbar.Children.Add(redoButton);
        bar.Children.Add(toolbar);

        exportsPanel.Orientation = Orientation.Horizontal;
        exportsPanel.Children.Add(Ui.Styled("IconButton", Ui.Icon("trash", 18), DeleteImage, "Remove from editor. Saved files are kept, and you can undo this.", "Remove from editor"));
        exportsPanel.Children.Add(Ui.Divider());
        var copy = Ui.Styled("Button", Ui.IconLabel("copy", "Copy", out var copyText), Copy, "Copy to clipboard  (Ctrl+C)", "Copy image");
        copy.Margin = new Thickness(0, 0, 8, 0); compactLabels.Add(copyText);
        exportsPanel.Children.Add(copy);
        var share = CreateShareButton(out var shareText);
        share.Margin = new Thickness(0, 0, 8, 0); compactLabels.Add(shareText);
        exportsPanel.Children.Add(share);
        exportsPanel.Children.Add(Ui.Styled("PrimaryButton", Ui.IconLabel("save", "Save", out _), Save, "Save a PNG to your screenshot folder  (Ctrl+S)", "Save PNG"));
        Grid.SetColumn(exportsPanel, 2); bar.Children.Add(exportsPanel);

        var host = new Border { BorderThickness = new Thickness(0, 1, 0, 1), Child = bar };
        host.SetResourceReference(Border.BackgroundProperty, "Surface");
        host.SetResourceReference(Border.BorderBrushProperty, "Line");
        return host;
    }

    private void ApplyCompact(bool compact)
    {
        foreach (var label in compactLabels) label.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---------------------------------------------------------------- notices

    private StackPanel BuildNotices()
    {
        var host = new StackPanel { Margin = new Thickness(20, 0, 20, 0) };

        shortcutNoticeText = Ui.Text("", 13, "Ink", wrap: true);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(Ui.Styled("Button", "Try again", () => { globalShortcut?.TryRegister(); UpdateShortcutButton(); }, "Check the shortcut again"));
        var change = Ui.Styled("Button", "Change shortcut", ChangeCaptureShortcut); change.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(change);
        shortcutNotice = BuildNotice("alert", "Warning", "WarningSoft", shortcutNoticeText, actions);
        shortcutNotice.Visibility = Visibility.Collapsed;
        host.Children.Add(shortcutNotice);

        undoDeleteButton = Ui.Styled("Button", "Undo", UndoDelete, "Bring the screenshot back into the editor");
        undoNotice = BuildNotice("info", "AccentText", "Selected", Ui.Text("Removed from the editor. Your saved files were not touched.", 13, "Ink", wrap: true), undoDeleteButton);
        undoNotice.Visibility = Visibility.Collapsed;
        host.Children.Add(undoNotice);
        return host;
    }

    private static Border BuildNotice(string icon, string iconBrush, string background, TextBlock message, UIElement actions)
    {
        var row = new DockPanel { LastChildFill = true };
        var glyph = Ui.Icon(icon, 18, iconBrush); glyph.Margin = new Thickness(0, 0, 12, 0);
        DockPanel.SetDock(glyph, Dock.Left); row.Children.Add(glyph);
        DockPanel.SetDock(actions, Dock.Right); ((FrameworkElement)actions).Margin = new Thickness(16, 0, 0, 0); row.Children.Add(actions);
        row.Children.Add(message);
        var card = new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Padding = new Thickness(14, 10, 10, 10), Margin = new Thickness(0, 12, 0, 0), Child = row };
        card.SetResourceReference(Border.BackgroundProperty, background);
        card.SetResourceReference(Border.BorderBrushProperty, "Line");
        return card;
    }

    /// <summary>The undo banner only makes sense while the editor is empty and a removed screenshot can still come back.</summary>
    private void RefreshUndoNotice()
    {
        bool visible = bitmap == null && deletedImage != null;
        undoDeleteButton.Visibility = undoNotice.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------------------------------------------------------------- home screen

    private FrameworkElement BuildHome()
    {
        var column = new StackPanel { MaxWidth = 880, Margin = new Thickness(32, 34, 32, 32) };
        column.Children.Add(Ui.Text("What would you like to capture?", 27, "Ink", FontWeights.SemiBold));
        var sub = Ui.Text("Every screenshot is saved automatically, so there's nothing to lose.", 14, "Muted");
        sub.Margin = new Thickness(0, 8, 0, 0); column.Children.Add(sub);
        homeHint = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        column.Children.Add(homeHint);

        var cards = new UniformGrid { Columns = 3, Margin = new Thickness(0, 26, 0, 0) };
        var modes = new[] {
            ("Area", "area", "Capture an area", "Drag to choose exactly what you want"),
            ("Window", "window", "Capture a window", "Click a window, even if something covers it"),
            ("Screen", "screen", "Capture full screen", "Grab everything on your main screen") };
        for (int i = 0; i < modes.Length; i++)
        {
            var (mode, icon, title, detail) = modes[i];
            var content = new StackPanel();
            var tile = new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(14), HorizontalAlignment = HorizontalAlignment.Left, Child = Ui.Icon(icon, 23, "AccentText", 1.6) };
            tile.SetResourceReference(Border.BackgroundProperty, "Selected");
            content.Children.Add(tile);
            var name = Ui.Text(title, 15.5, "Ink", FontWeights.SemiBold); name.Margin = new Thickness(0, 18, 0, 0); content.Children.Add(name);
            var about = Ui.Text(detail, 12.5, "Muted", wrap: true); about.Margin = new Thickness(0, 5, 0, 0); content.Children.Add(about);
            var card = Ui.Styled("CardButton", content, async () => await Take(mode), null, title);
            card.Margin = new Thickness(i == 0 ? 0 : 7, 0, i == modes.Length - 1 ? 0 : 7, 0);
            card.ToolTip = title;
            cards.Children.Add(card);
        }
        column.Children.Add(cards);

        var recentHeader = new DockPanel { Margin = new Thickness(0, 34, 0, 12) };
        var folder = Ui.Styled("LinkButton", Ui.IconLabel("folder", "Open folder", out _, 16), OpenScreenshotFolder, "Open your screenshot folder in File Explorer", "Open screenshot folder");
        DockPanel.SetDock(folder, Dock.Right); recentHeader.Children.Add(folder);
        recentHeader.Children.Add(Ui.Text("Recent screenshots", 15, "Ink", FontWeights.SemiBold));
        recentGrid = new UniformGrid { Columns = 4 };
        recentEmpty = BuildRecentEmpty();
        var recent = new StackPanel();
        recent.Children.Add(recentHeader); recent.Children.Add(recentGrid); recent.Children.Add(recentEmpty);
        recentSection = recent; column.Children.Add(recent);

        return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = column };
    }

    private FrameworkElement BuildRecentEmpty()
    {
        var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var glyph = Ui.Icon("image", 28, "Subtle", 1.5); glyph.HorizontalAlignment = HorizontalAlignment.Center; content.Children.Add(glyph);
        var title = Ui.Text("Your screenshots will show up here", 13.5, "Ink", FontWeights.SemiBold); title.Margin = new Thickness(0, 12, 0, 0); title.HorizontalAlignment = HorizontalAlignment.Center; content.Children.Add(title);
        var hint = Ui.Text("Take one and it's saved and ready to open again.", 12.5, "Muted"); hint.Margin = new Thickness(0, 4, 0, 0); hint.HorizontalAlignment = HorizontalAlignment.Center; content.Children.Add(hint);
        var frame = Ui.Card(content, 16, new Thickness(24, 34, 24, 34));
        frame.Background = Brushes.Transparent;
        frame.Visibility = Visibility.Collapsed;
        return frame;
    }

    /// <summary>Loads the latest saved screenshots off the UI thread and shows them as thumbnails.</summary>
    private void RefreshRecents()
    {
        int version = ++recentVersion;
        if (!preferences.ShowRecents) { recentSection.Visibility = Visibility.Collapsed; return; }
        recentSection.Visibility = Visibility.Visible;
        string folder = preferences.SaveFolder;
        Task.Run(() => Recents.Find(folder, 8).Select(item => (Item: item, Image: Recents.Thumbnail(item.Path, 420))).Where(pair => pair.Image != null).ToList())
            .ContinueWith(task => {
                if (task.IsFaulted) return;
                Dispatcher.BeginInvoke(new Action(() => ShowRecents(version, task.Result)));
            }, TaskScheduler.Default);
    }

    private void ShowRecents(int version, List<(RecentItem Item, BitmapSource? Image)> found)
    {
        if (version != recentVersion) return;
        recentGrid.Children.Clear();
        recentEmpty.Visibility = found.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        for (int i = 0; i < found.Count; i++)
        {
            var (item, image) = found[i];
            var picture = new Border { CornerRadius = new CornerRadius(15), Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill, AlignmentY = AlignmentY.Top } };
            var stamp = new TextBlock { Text = Recents.Describe(item.Time), FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(11, 0, 11, 8), VerticalAlignment = VerticalAlignment.Bottom };
            var shade = new Border {
                CornerRadius = new CornerRadius(0, 0, 15, 15), Height = 46, VerticalAlignment = VerticalAlignment.Bottom,
                Background = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(200, 0, 0, 0), 90)
            };
            var layers = new Grid { Height = 120 };
            layers.Children.Add(picture); layers.Children.Add(shade); layers.Children.Add(stamp);
            var thumb = Ui.Styled("CardButton", layers, () => OpenRecent(item), "Open in the editor", "Open screenshot from " + Recents.Describe(item.Time));
            thumb.Padding = new Thickness(0); thumb.Margin = new Thickness(i % 4 == 0 ? 0 : 6, i < 4 ? 0 : 12, i % 4 == 3 ? 0 : 6, 0);
            thumb.ToolTip = "Open in the editor";
            recentGrid.Children.Add(thumb);
        }
    }

    // ---------------------------------------------------------------- editor view

    private FrameworkElement BuildEditView()
    {
        var view = new Grid();
        view.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        view.RowDefinitions.Add(new RowDefinition());
        var options = BuildOptionsBar(); view.Children.Add(options);
        var frame = new Border { CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), Padding = new Thickness(16), Margin = new Thickness(20, 0, 20, 12), Child = stage };
        frame.SetResourceReference(Border.BackgroundProperty, "Canvas");
        frame.SetResourceReference(Border.BorderBrushProperty, "Line");
        Grid.SetRow(frame, 1); view.Children.Add(frame);
        return view;
    }

    /// <summary>Row above the image that shows only what the active tool can change.</summary>
    private FrameworkElement BuildOptionsBar()
    {
        var bar = new Grid { Margin = new Thickness(24, 8, 20, 6), MinHeight = 36 };
        bar.ColumnDefinitions.Add(new ColumnDefinition());
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolHint = Ui.Text("", 12.5, "Muted"); toolHint.TextTrimming = TextTrimming.CharacterEllipsis; toolHint.Margin = new Thickness(0, 0, 16, 0);
        bar.Children.Add(toolHint);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(controls, 1); bar.Children.Add(controls);

        colorOptions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var colorLabel = Ui.Text("Color", 12.5, "Muted"); colorLabel.Margin = new Thickness(0, 0, 8, 0); colorOptions.Children.Add(colorLabel);
        foreach (var (name, color) in Swatches)
        {
            var swatch = new RadioButton { GroupName = "annotationColor", Background = new SolidColorBrush(color), ToolTip = name };
            swatch.SetResourceReference(FrameworkElement.StyleProperty, "SwatchButton");
            System.Windows.Automation.AutomationProperties.SetName(swatch, name);
            var chosen = color; swatch.Checked += (_, _) => { if (!syncingSwatches) PickColor(chosen); };
            swatchButtons.Add((swatch, color)); colorOptions.Children.Add(swatch);
        }
        controls.Children.Add(colorOptions);

        var thickness = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var thicknessLabel = Ui.Text("Size", 12.5, "Muted"); thicknessLabel.Margin = new Thickness(0, 0, 8, 0); thickness.Children.Add(thicknessLabel);
        var pill = new StackPanel { Orientation = Orientation.Horizontal };
        string[] sizeNames = { "Thin", "Medium", "Thick" };
        for (int i = 0; i < sizeNames.Length; i++)
        {
            int level = i;
            var item = new RadioButton { GroupName = "thickness", Content = Ui.Icon("line", 22, null, 1.6 + 1.5 * i), ToolTip = sizeNames[i], IsChecked = i == thicknessLevel };
            item.SetResourceReference(FrameworkElement.StyleProperty, "SegmentItem");
            item.Height = 30; item.MinWidth = 0; item.Padding = new Thickness(7, 0, 7, 0);
            System.Windows.Automation.AutomationProperties.SetName(item, sizeNames[i] + " line");
            item.Checked += (_, _) => { thicknessLevel = level; };
            thicknessButtons.Add(item); pill.Children.Add(item);
        }
        var track = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(2), Child = pill };
        track.SetResourceReference(Border.BackgroundProperty, "Field");
        thickness.Children.Add(track);
        thicknessOptions = thickness;
        controls.Children.Add(thickness);

        BuildTextTools();
        controls.Children.Add(textTools);
        return bar;
    }

    // ---------------------------------------------------------------- status bar

    private FrameworkElement BuildStatusBar()
    {
        var dock = new DockPanel { Margin = new Thickness(20, 0, 20, 0), Height = 38 };
        shortcutButton = Ui.Styled("GhostButton", "", () => { if (globalShortcut?.Registered == true) ChangeCaptureShortcut(); else { globalShortcut?.TryRegister(); UpdateShortcutButton(); } });
        shortcutButton.MinHeight = 30; shortcutButton.Padding = new Thickness(8, 0, 8, 0);
        DockPanel.SetDock(shortcutButton, Dock.Right); dock.Children.Add(shortcutButton);
        statusFolder = Ui.Styled("GhostButton", Ui.IconLabel("folder", "Open folder", out _, 16), OpenScreenshotFolder, "Open your screenshot folder in File Explorer", "Open screenshot folder");
        statusFolder.MinHeight = 30; statusFolder.Padding = new Thickness(8, 0, 8, 0); statusFolder.Margin = new Thickness(0, 0, 4, 0);
        DockPanel.SetDock(statusFolder, Dock.Right); dock.Children.Add(statusFolder);

        saveDot = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
        saveText = Ui.Text("Saved", 12, "Muted");
        saveState = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 0) };
        saveState.Children.Add(saveDot); saveState.Children.Add(saveText);
        DockPanel.SetDock(saveState, Dock.Left); dock.Children.Add(saveState);
        sizeLabel = Ui.Text("", 12, "Muted"); sizeLabel.Margin = new Thickness(12, 0, 0, 0);
        DockPanel.SetDock(sizeLabel, Dock.Left); dock.Children.Add(sizeLabel);

        status.FontSize = 12; status.SetResourceReference(ForegroundProperty, "Muted"); status.VerticalAlignment = VerticalAlignment.Center;
        status.TextTrimming = TextTrimming.CharacterEllipsis; status.Margin = new Thickness(14, 0, 12, 0);
        dock.Children.Add(status);
        ApplyShortcutState(true);

        var host = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = dock };
        host.SetResourceReference(Border.BorderBrushProperty, "Line");
        return host;
    }

    private void UpdateShortcutButton() => ApplyShortcutState(globalShortcut?.Registered == true);

    private void ApplyShortcutState(bool active)
    {
        string label = preferences.Shortcut.Label;
        var chip = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (active)
        {
            chip.Children.Add(Ui.Icon("keyboard", 16));
            var caps = Ui.Kbd(label, 11); caps.Margin = new Thickness(8, 0, 0, 0); chip.Children.Add(caps);
        }
        else
        {
            chip.Children.Add(Ui.Icon("alert", 16, "Warning"));
            var warning = Ui.Text("Shortcut unavailable", 12, "Warning", FontWeights.SemiBold); warning.Margin = new Thickness(7, 0, 0, 0); chip.Children.Add(warning);
        }
        shortcutButton.Content = chip;
        System.Windows.Automation.AutomationProperties.SetName(shortcutButton, active ? "Capture shortcut " + label : "Capture shortcut unavailable");
        shortcutButton.ToolTip = active
            ? $"Press {label} in any app to capture an area, even while SnipCanvas is in the tray. Click to change it."
            : $"{label} is being used by another app. Click to try again, or choose another shortcut in Settings.";
        shortcutNotice.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        shortcutNoticeText.Text = $"{label} isn't available right now. Another app or Windows itself may be using it.";
        areaMenuItem.InputGestureText = label;

        homeHint.Children.Clear();
        if (active)
        {
            homeHint.Children.Add(Ui.Text("Tip: press", 13, "Muted"));
            var caps = Ui.Kbd(label, 11.5); caps.Margin = new Thickness(8, 0, 8, 0); homeHint.Children.Add(caps);
            homeHint.Children.Add(Ui.Text("in any app to capture an area.", 13, "Muted"));
        }
        if (tray != null) tray.Text = "SnipCanvas · " + label;
        if (trayMenu?.Items[1] is System.Windows.Forms.ToolStripMenuItem capture) capture.ShortcutKeyDisplayString = label;
    }

#if SNIPCANVAS_DIAGNOSTICS
    internal void PreviewShortcutState(bool active) => ApplyShortcutState(active);
    internal ContextMenu CaptureMenu => captureMenu;
    internal void PreviewToast(string message) { Toast(message); toast.BeginAnimation(OpacityProperty, null); toast.Opacity = 1; ((TranslateTransform)toast.RenderTransform).BeginAnimation(TranslateTransform.YProperty, null); }
#endif

    // ---------------------------------------------------------------- state -> chrome

    private void SetHasImage(bool has)
    {
        bool changed = editView.Visibility != (has ? Visibility.Visible : Visibility.Collapsed);
        editView.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        homeView.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        commandBar.Visibility = newShotSplit.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        saveState.Visibility = sizeLabel.Visibility = statusFolder.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        exportsPanel.IsEnabled = toolbar.IsEnabled = has;
        RefreshUndoNotice();
        if (!changed) return;
        Fade(has ? editView : homeView);
        if (!has) { status.Text = ""; RefreshRecents(); }
    }

    private static void Fade(UIElement element)
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
    }

    /// <summary>Keeps the undo/redo buttons and the saved indicator in step with the document.</summary>
    private void RefreshChrome()
    {
        undoButton.IsEnabled = undo.Count > 0;
        redoButton.IsEnabled = redo.Count > 0;
        UpdateSaveState();
    }

    private void UpdateSaveState()
    {
        if (saveDot == null) return;
        saveDot.SetResourceReference(Shape.FillProperty, dirty ? "Warning" : "Success");
        saveText.Text = dirty ? "Unsaved edits" : "Saved";
        saveState.ToolTip = dirty
            ? "Your edits save automatically when you take the next screenshot or quit. Press Ctrl+S to save them now."
            : "This screenshot is saved in " + preferences.SaveFolder;
    }

    private void PickColor(Color color)
    {
        annotationColor = color;
        if (tool == "Text" && selectedText >= 0 && imageEdits[selectedText] is TextEdit note && note.Color != color)
        {
            undo.Push(Snapshot()); redo.Clear();
            imageEdits[selectedText] = note with { Color = color };
            RenderDocument(); dirty = true;
        }
    }

    private bool syncingSwatches;
    private void SyncSwatches(Color color)
    {
        syncingSwatches = true;
        try { foreach (var (button, swatch) in swatchButtons) button.IsChecked = swatch == color; }
        finally { syncingSwatches = false; }
    }

    private void UpdateOptions()
    {
        var note = tool == "Text" && bitmap != null && selectedText >= 0 && selectedText < imageEdits.Count ? imageEdits[selectedText] as TextEdit : null;
        bool textSelected = note != null;
        colorOptions.Visibility = tool is "Arrow" or "Box" or "Text" ? Visibility.Visible : Visibility.Collapsed;
        thicknessOptions.Visibility = tool is "Arrow" or "Box" ? Visibility.Visible : Visibility.Collapsed;
        textTools.Visibility = textSelected ? Visibility.Visible : Visibility.Collapsed;
        toolHint.Text = tool switch
        {
            "Arrow" => "Drag on the screenshot to draw an arrow.",
            "Box" => "Drag to draw a box around something.",
            "Text" => textSelected ? "Drag the text to move it. Double-click to change the words." : "Click where you want text. Click existing text to edit or move it.",
            "Blur" => "Drag over anything you want to hide, like names or numbers.",
            _ => "Drag around the part you want to keep."
        };
        SyncSwatches(note?.Color ?? annotationColor);
    }

    // ---------------------------------------------------------------- toast

    private Border BuildToast()
    {
        toastIcon = new ContentControl { Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        toastText = Ui.Text("", 13, "Ink", FontWeights.SemiBold);
        toastAction = Ui.Styled("LinkButton", "", () => { var handler = toastActionHandler; HideToast(); handler?.Invoke(); });
        toastAction.Margin = new Thickness(10, 0, -6, 0); toastAction.Visibility = Visibility.Collapsed;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(toastIcon); row.Children.Add(toastText); row.Children.Add(toastAction);
        var pill = new Border {
            CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Padding = new Thickness(16, 10, 16, 10), Child = row,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 26),
            Visibility = Visibility.Collapsed, RenderTransform = new TranslateTransform(),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 20, ShadowDepth = 4, Opacity = 0.35, Color = Colors.Black }
        };
        pill.SetResourceReference(Border.BackgroundProperty, "Surface");
        pill.SetResourceReference(Border.BorderBrushProperty, "LineStrong");
        return pill;
    }

    /// <summary>Brief confirmation that floats over the editor (copied, saved, and so on).</summary>
    private void Toast(string message, string icon = "check", string iconBrush = "Success", string? actionLabel = null, Action? action = null)
    {
        toastTimer?.Stop();
        toastIcon.Content = Ui.Icon(icon, 17, iconBrush, 2);
        toastText.Text = message;
        toastActionHandler = action;
        toastAction.Content = actionLabel ?? ""; toastAction.Visibility = actionLabel == null ? Visibility.Collapsed : Visibility.Visible;
        toast.Visibility = Visibility.Visible;
        if (SystemParameters.ClientAreaAnimation)
        {
            var slide = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            ((TranslateTransform)toast.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slide);
            toast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        }
        else { toast.BeginAnimation(OpacityProperty, null); toast.Opacity = 1; }
        toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(actionLabel == null ? Math.Clamp(1800 + message.Length * 45, 2600, 7000) : 6000) };
        toastTimer.Tick += (_, _) => HideToast();
        toastTimer.Start();
    }

    private void HideToast()
    {
        toastTimer?.Stop();
        if (toast.Visibility != Visibility.Visible) return;
        if (!SystemParameters.ClientAreaAnimation) { toast.Visibility = Visibility.Collapsed; return; }
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        fade.Completed += (_, _) => { if (toastTimer?.IsEnabled != true) toast.Visibility = Visibility.Collapsed; };
        toast.BeginAnimation(OpacityProperty, fade);
    }

    // ---------------------------------------------------------------- theme

    private static string ReadThemeMode()
    {
        try
        {
            if (!File.Exists(ThemeFile)) return "system";
            string saved = File.ReadAllText(ThemeFile).Trim();
            return saved is "dark" or "light" ? saved : "system";
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return "system"; }
    }

    private bool ResolveDark() => themeMode == "system" ? Theme.SystemPrefersDark() : themeMode == "dark";

    /// <summary>Applies an explicit light or dark theme (used by tests and previews).</summary>
    internal void ChangeTheme(bool useDark, bool persist = false) => SetThemeMode(useDark ? "dark" : "light", persist);

    internal void SetThemeMode(string mode, bool persist = false)
    {
        themeMode = mode;
        Theme.Apply(ResolveDark());
        ApplyTitleTheme(); ApplyTrayTheme();
        if (!persist) return;
        try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ThemeFile)!); File.WriteAllText(ThemeFile, themeMode); }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { Dialogs.Problem(this, "Your theme choice couldn't be remembered", "The new theme works for now, but SnipCanvas will go back to the old one next time.", ex.Message); }
    }

    private void ApplyTitleTheme() => Theme.StyleTitleBar(this);

    private void FollowSystemTheme(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (themeMode != "system" || e.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
        Dispatcher.BeginInvoke(new Action(() => SetThemeMode("system")));
    }
}
