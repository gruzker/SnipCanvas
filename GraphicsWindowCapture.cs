using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;

namespace SnipCanvas;

internal static class GraphicsWindowCapture
{
    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(IntPtr window, ref Guid iid);
        IntPtr CreateForMonitor(IntPtr monitor, ref Guid iid);
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags,
        IntPtr featureLevels, uint featureLevelCount, uint sdkVersion, out IntPtr device, out int featureLevel, out IntPtr context);
    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr device);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr handle);

    internal static Task<BitmapSource> TakeAsync(IntPtr handle, CancellationToken cancellation = default)
        => Task.Run(() => CaptureAsync(handle, cancellation), cancellation);

    private static async Task<BitmapSource> CaptureAsync(IntPtr handle, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!IsWindow(handle) || IsIconic(handle))
            throw new InvalidOperationException("The selected window is no longer available. Select an open window and try again.");
        if (!GraphicsCaptureSession.IsSupported())
            throw new InvalidOperationException("Window capture is unavailable on this Windows device. Try capturing an area instead.");

        var iid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
        var pointer = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>().CreateForWindow(handle, ref iid);
        GraphicsCaptureItem item;
        try { item = WinRT.MarshalInterface<GraphicsCaptureItem>.FromAbi(pointer); }
        finally { Marshal.Release(pointer); }
        if (item.Size.Width <= 0 || item.Size.Height <= 0)
            throw new InvalidOperationException("The selected window has no content to capture.");

        using var device = CreateDevice();
        using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
        using var session = pool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;
        session.StartCapture();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        Direct3D11CaptureFrame? latest = null;
        try
        {
            while (timer.Elapsed < TimeSpan.FromSeconds(5))
            {
                cancellation.ThrowIfCancellationRequested();
                // The free-threaded pool receives compositor frames without blocking the editor's UI.
                var next = pool.TryGetNextFrame();
                if (next != null) { latest?.Dispose(); latest = next; }
                // Chromium can initially supply its blank occlusion placeholder. Give the compositor
                // time to resume video rendering, draining early frames instead of saving the first one.
                if (latest == null || timer.ElapsedMilliseconds < 250) { await Task.Delay(16, cancellation).ConfigureAwait(false); continue; }
                var frame = latest;
                int width = frame.ContentSize.Width, height = frame.ContentSize.Height;
                if (width <= 0 || height <= 0) { await Task.Delay(16, cancellation).ConfigureAwait(false); continue; }
                using var software = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface, BitmapAlphaMode.Ignore);
                cancellation.ThrowIfCancellationRequested();
                if (width > software.PixelWidth || height > software.PixelHeight)
                    throw new InvalidOperationException("The window changed size during capture. Try again.");
                var pixels = new byte[checked(software.PixelWidth * software.PixelHeight * 4)];
                software.CopyToBuffer(pixels.AsBuffer());
                // Bgr32 ignores surface alpha, preserving video and ordinary window pixels as opaque.
                var image = BitmapSource.Create(software.PixelWidth, software.PixelHeight, 96, 96,
                    PixelFormats.Bgr32, null, pixels, checked(software.PixelWidth * 4));
                BitmapSource result = image;
                if (width != image.PixelWidth || height != image.PixelHeight)
                    result = new CroppedBitmap(image, new System.Windows.Int32Rect(0, 0, width, height));
                result.Freeze();
                return result;
            }
            throw new TimeoutException("The window did not provide a screenshot. Keep it open and try again.");
        }
        finally { latest?.Dispose(); }
    }

    private static IDirect3DDevice CreateDevice()
    {
        IntPtr native = IntPtr.Zero, context = IntPtr.Zero, dxgi = IntPtr.Zero, wrapper = IntPtr.Zero;
        try
        {
            const uint bgraSupport = 0x20;
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(IntPtr.Zero, 1, IntPtr.Zero, bgraSupport,
                IntPtr.Zero, 0, 7, out native, out _, out context));
            var iid = new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(native, ref iid, out dxgi));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out wrapper));
            return WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(wrapper);
        }
        finally
        {
            if (wrapper != IntPtr.Zero) Marshal.Release(wrapper);
            if (dxgi != IntPtr.Zero) Marshal.Release(dxgi);
            if (context != IntPtr.Zero) Marshal.Release(context);
            if (native != IntPtr.Zero) Marshal.Release(native);
        }
    }
}
