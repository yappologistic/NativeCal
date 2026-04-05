using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Set DPI awareness FIRST so GetWindowRect returns physical pixels
NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4)); // PER_MONITOR_AWARE_V2

uint pid = uint.Parse(args[0]);
string outPath = args[1];
IntPtr foundHwnd = IntPtr.Zero;

NativeMethods.EnumWindows((h, l) => {
    NativeMethods.GetWindowThreadProcessId(h, out uint p);
    if (p == pid && NativeMethods.IsWindowVisible(h)) {
        NativeMethods.GetWindowRect(h, out NativeMethods.RECT r2);
        if (r2.R - r2.L > 100) { foundHwnd = h; return false; }
    }
    return true;
}, IntPtr.Zero);

if (foundHwnd == IntPtr.Zero) { Console.WriteLine("NOT_FOUND"); return; }
NativeMethods.SetForegroundWindow(foundHwnd);
Thread.Sleep(500);
NativeMethods.GetWindowRect(foundHwnd, out NativeMethods.RECT rect);
int w = rect.R - rect.L, h2 = rect.B - rect.T;
using var bmp = new Bitmap(w, h2);
using (var g = Graphics.FromImage(bmp)) {
    g.CopyFromScreen(rect.L, rect.T, 0, 0, new Size(w, h2));
}
bmp.Save(outPath, ImageFormat.Png);
Console.WriteLine($"OK {w}x{h2}");

static partial class NativeMethods {
    public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int L, T, R, B; }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumWindows(EnumWindowsProc d, IntPtr l);

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(IntPtr h, out uint p);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(IntPtr h);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(IntPtr h, out RECT r);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(IntPtr h);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetProcessDpiAwarenessContext(IntPtr value);
}
