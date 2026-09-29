using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace SnipCanvas;

internal sealed partial class MainWindow
{
    private DataTransferManager? shareManager;
    private StorageFile? shareFile;
    private bool preparingShare;

    [ComImport, Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataTransferManagerInterop
    {
        IntPtr GetForWindow(IntPtr window, ref Guid iid);
        void ShowShareUIForWindow(IntPtr window);
    }

    private Button CreateShareButton(out TextBlock label)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(Ui.Icon("share", 17));
        label = new TextBlock { Text = "Share", Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(label);
        var chevron = Ui.Icon("chevron-down", 14); chevron.Margin = new Thickness(6, 0, -2, 0); content.Children.Add(chevron);
        var button = Ui.Styled("Button", content, null, "Share your edited screenshot", "Share");

        var menu = new ContextMenu();
        void Add(string header, string hint, string icon, string color, Action action)
        {
            var item = new MenuItem { Header = header, ToolTip = hint, Icon = MenuTile(icon, color) };
            item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        Add("Windows Share…", "Choose a compatible installed app, including email apps.", "share", "#2F7DE1", async () => await ShareWithWindows());
        menu.Items.Add(new Separator());
        Add("WhatsApp Web…", "Copy the screenshot and open WhatsApp Web. Choose a chat and press Ctrl+V.", "chat", "#1FAF54", () => ShareToWeb("https://web.whatsapp.com/", "WhatsApp"));
        Add("Telegram Web…", "Copy the screenshot and open Telegram Web. Choose a chat and press Ctrl+V.", "plane", "#2A9BD8", () => ShareToWeb("https://web.telegram.org/", "Telegram"));
        Add("Email…", "Open a draft in your default email app. Paste or attach the screenshot.", "mail", "#7C5CE0", () => ShareToWeb("mailto:?subject=Screenshot", "your email draft"));
        menu.Items.Add(new Separator());
        Add("Show image file…", "Open the PNG in its folder so you can attach or drag it into another app.", "folder", "#C58A1B", ShowShareFile);
        button.ContextMenu = menu;
        button.Click += (_, _) => OpenMenuUnder(menu, button);
        return button;
    }

#if SNIPCANVAS_DIAGNOSTICS
    internal void RenderShareMenuPreview(string path) => RenderMenuPreview(CreateShareButton(out _).ContextMenu, path);

    internal void RenderMenuPreview(ContextMenu menu, string path)
    {
        menu.PlacementTarget = this;
        menu.IsOpen = true;
        try
        {
            menu.Dispatcher.Invoke(() => menu.UpdateLayout(), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var rendered = new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth), (int)Math.Ceiling(menu.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            var paper = new DrawingVisual();
            using (var dc = paper.RenderOpen()) dc.DrawRectangle(Background, null, new Rect(0, 0, rendered.PixelWidth, rendered.PixelHeight));
            rendered.Render(paper); rendered.Render(menu);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(rendered));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var stream = File.Create(path); png.Save(stream);
        }
        finally { menu.IsOpen = false; }
    }

#endif
    internal static string WriteShareImage(BitmapSource image)
    {
        string folder = Path.Combine(Path.GetTempPath(), "SnipCanvas", "SharedImages");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "SnipCanvas-" + Guid.NewGuid().ToString("N") + ".png");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path); encoder.Save(stream); return path;
    }

    private string CopyForSharing()
    {
        var image = bitmap ?? throw new InvalidOperationException("Capture an image first.");
        string path = WriteShareImage(image);
        var data = new System.Windows.DataObject();
        data.SetImage(image);
        data.SetFileDropList(new StringCollection { path });
        System.Windows.Clipboard.SetDataObject(data, true);
        return path;
    }

    private void ShareToWeb(string url, string destination)
    {
        if (bitmap == null) return;
        try
        {
            CopyForSharing();
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            Toast(destination == "your email draft"
                ? "Copied. Paste it into your email, or use Share > Show image file to attach it."
                : "Copied. Choose a chat in " + destination + " and press Ctrl+V.");
        }
        catch (Exception ex) { ShareError(ex); }
    }

    private void ShowShareFile()
    {
        if (bitmap == null) return;
        try
        {
            RevealFile(WriteShareImage(bitmap));
            Toast("Drag the selected PNG into a chat, or attach it to an email.", "info", "AccentText");
        }
        catch (Exception ex) { ShareError(ex); }
    }

    private async Task ShareWithWindows()
    {
        if (bitmap == null || preparingShare) return;
        preparingShare = true;
        try
        {
            var image = bitmap;
            string path = WriteShareImage(image);
            shareFile = await StorageFile.GetFileFromPathAsync(path);
            var interop = InitializeSharing();
            var handle = new WindowInteropHelper(this).Handle;
            interop.ShowShareUIForWindow(handle);
        }
        catch (Exception ex) { ShareError(ex); }
        finally { preparingShare = false; }
    }

    private IDataTransferManagerInterop InitializeSharing()
    {
        var interop = DataTransferManager.As<IDataTransferManagerInterop>();
        if (shareManager == null)
        {
            var iid = new Guid("A5CAEE9B-8708-49D1-8D36-67D25A8DA00C");
            var pointer = interop.GetForWindow(new WindowInteropHelper(this).Handle, ref iid);
            try { shareManager = WinRT.MarshalInterface<DataTransferManager>.FromAbi(pointer); }
            finally { Marshal.Release(pointer); }
            shareManager.DataRequested += ProvideShareData;
        }
        return interop;
    }

#if SNIPCANVAS_DIAGNOSTICS
    internal void VerifyShareIntegration() => InitializeSharing();
#endif

    private void ProvideShareData(DataTransferManager sender, DataRequestedEventArgs args)
    {
        try
        {
            var file = shareFile;
            if (file == null) { args.Request.FailWithDisplayText("Capture an image, then choose Share again."); return; }
            args.Request.Data.Properties.Title = "SnipCanvas screenshot";
            args.Request.Data.RequestedOperation = DataPackageOperation.Copy;
            args.Request.Data.SetStorageItems(new[] { file }, true);
            args.Request.Data.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
        }
        catch { args.Request.FailWithDisplayText("The screenshot could not be prepared. Try Share > Show image file."); }
    }

    private void ShareError(Exception ex) => Dialogs.Problem(this, "Sharing didn't work", "You can still use Copy or Save and attach the screenshot yourself.", ex.Message);
    private void DisposeSharing() { if (shareManager != null) shareManager.DataRequested -= ProvideShareData; }
}
