// Developer ::> Gehan Fernando
using System.Runtime.InteropServices;
using ModernCalculator.WinForms.Controls;
using ModernCalculator.WinForms.Themes;

namespace ModernCalculator.WinForms.Forms;

/// <summary>Custom client frame with native Windows move, resize and system commands.</summary>
public class BorderlessForm : Form
{
    private const int WmNcCalcSize = 0x83, WmNcHitTest = 0x84, WmGetMinMaxInfo = 0x24;
    private const int HtCaption = 2;
    protected virtual bool ResizableFrame => true;
    public BorderlessForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        Padding = new(6);
    }
    // Retain native window styles while drawing the frame in the client area.
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams; parameters.Style |= 0x00C00000 | 0x00080000;
            if (ResizableFrame) parameters.Style |= 0x00040000;
            if (MinimizeBox) parameters.Style |= 0x00020000;
            if (MaximizeBox) parameters.Style |= 0x00010000;
            return parameters;
        }
    }
    public void RegisterDragSurface(Control surface)
    {
        surface.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.Clicks == 2 && MaximizeBox) ToggleMaximize();
            else
            {
                var point = surface.PointToScreen(e.Location);
                var packed = (nint)((point.X & 0xffffL) | ((point.Y & 0xffffL) << 16));
                ReleaseCapture(); SendMessage(Handle, 0xA1, HtCaption, packed);
            }
        };
    }
    public void ToggleMaximize() => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        { var preference = 2; _ = DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int)); }
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmNcCalcSize) { message.Result = 0; return; }
        if (message.Msg == WmNcHitTest && ResizableFrame && WindowState == FormWindowState.Normal)
        {
            var packed = message.LParam.ToInt64();
            var point = PointToClient(new((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)));
            var edge = Math.Max(5, 6 * DeviceDpi / 96);
            var left = point.X < edge; var right = point.X >= ClientSize.Width - edge;
            var top = point.Y < edge; var bottom = point.Y >= ClientSize.Height - edge;
            var hit = top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17 : left ? 10 : right ? 11 : top ? 12 : bottom ? 15 : 0;
            if (hit != 0) { message.Result = hit; return; }
        }
        base.WndProc(ref message);
        if (message.Msg == WmGetMinMaxInfo && message.LParam != 0)
        {
            var monitor = MonitorFromWindow(Handle, 2);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info)) return;
            var bounds = Marshal.PtrToStructure<MinMaxInfo>(message.LParam);
            bounds.MaximumPosition = new(info.Work.Left - info.Monitor.Left, info.Work.Top - info.Monitor.Top);
            bounds.MaximumSize = new(info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top);
            bounds.MinimumTrack = new(MinimumSize.Width, MinimumSize.Height);
            Marshal.StructureToPtr(bounds, message.LParam, false);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; public NativePoint(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public NativePoint Reserved, MaximumSize, MaximumPosition, MinimumTrack, MaximumTrack; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public int Flags; }
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern nint SendMessage(nint window, int message, nint wparam, nint lparam);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("dwmapi.dll")] protected static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
