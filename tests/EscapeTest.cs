using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SnipCanvas;

internal static partial class EscapeTest
{
    public static void Run()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var report = new List<string>();
        try
        {
            using var image = new Bitmap(120, 80);
            var bounds = new Rectangle(-30000, -30000, 120, 80);
            void Check(bool condition, string message)
            { if (!condition) throw new InvalidOperationException(message); }
            void Escape(CaptureCancellation scope)
            {
                var data = Marshal.AllocHGlobal(24);
                try
                {
                    Marshal.WriteInt32(data, 0x1B);
                    var result = (IntPtr)typeof(CaptureCancellation).GetMethod("OnKeyboard", flags)!
                        .Invoke(scope, new object[] { 0, new IntPtr(0x100), data })!;
                    Check(result == new IntPtr(1), "Escape must be consumed during capture");
                }
                finally { Marshal.FreeHGlobal(data); }
            }
            using (var early = new CaptureCancellation())
            {
                Escape(early);
                try { Capture.Take("Area", early.Token).GetAwaiter().GetResult(); throw new Exception("Early Escape was ignored"); }
                catch (OperationCanceledException) { }
                try { GraphicsWindowCapture.TakeAsync(IntPtr.Zero, early.Token).GetAwaiter().GetResult(); throw new Exception("Window capture ignored cancellation"); }
                catch (OperationCanceledException) { }
                report.Add("PASS: Escape before selection cancels area and window capture.");
            }
            for (int scenario = 0; scenario < 5; scenario++)
            {
                using var scope = new CaptureCancellation();
                using var overlay = new SelectionOverlay(image, bounds, new(), scenario == 3, scope.Token);
                using var otherWindow = new Form { ShowInTaskbar = false, Bounds = bounds, StartPosition = FormStartPosition.Manual };
                bool timedOut = false;
                using var timeout = new System.Windows.Forms.Timer { Interval = 3000 };
                timeout.Tick += (_, _) => { timedOut = true; overlay.Close(); };
                overlay.Shown += (_, _) => overlay.BeginInvoke(new Action(() => {
                    if (scenario == 0)
                    {
                        var message = new Message();
                        Check((bool)typeof(SelectionOverlay).GetMethod("ProcessCmdKey", flags)!
                            .Invoke(overlay, new object[] { message, Keys.Escape })!, "Dialog Escape must be handled");
                    }
                    else
                    {
                        typeof(Control).GetMethod("OnMouseDown", flags)!.Invoke(overlay,
                            new object[] { new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0) });
                        typeof(Control).GetMethod("OnMouseMove", flags)!.Invoke(overlay,
                            new object[] { new MouseEventArgs(MouseButtons.Left, 0, 70, 50, 0) });
                        if (scenario == 4)
                            typeof(Control).GetMethod("OnMouseUp", flags)!.Invoke(overlay,
                                new object[] { new MouseEventArgs(MouseButtons.Left, 1, 70, 50, 0) });
                        else
                        {
                            if (scenario == 2) { otherWindow.Show(); otherWindow.Activate(); }
                            Escape(scope);
                        }
                    }
                }));
                timeout.Start();
                var result = overlay.ShowDialog();
                timeout.Stop();
                Check(!timedOut, "Selection did not close promptly in scenario " + scenario);
                Check(!overlay.Capture, "Selection retained mouse capture");
                Check(result == (scenario == 4 ? DialogResult.OK : DialogResult.Cancel), "Unexpected selection result");
                if (scenario != 4) Check(overlay.Selection.IsEmpty, "Cancelled selection retained pixels");
                else Check(overlay.Selection.Size == new Size(60, 40), "Next capture did not work");
                report.Add("PASS: " + new[] { "Dialog Escape", "Escape during drag", "Escape with another window active", "Window selection Escape", "Successful capture after cancellation" }[scenario]);
            }
            File.WriteAllLines("escape-test-result.txt", report);
        }
        catch (Exception ex) { File.WriteAllText("escape-test-result.txt", string.Join(Environment.NewLine, report) + Environment.NewLine + ex); Environment.ExitCode = 1; }
    }
}
