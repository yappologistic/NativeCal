using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

class Program {
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc d, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L,T,R,B; }
    delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
    
    static IntPtr foundHwnd;
    static void Main(string[] args) {
        uint pid = uint.Parse(args[0]);
        string outPath = args[1];
        EnumWindows((h,l) => {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == pid && IsWindowVisible(h)) {
                RECT r; GetWindowRect(h, out r);
                if (r.R - r.L > 100) { foundHwnd = h; return false; }
            }
            return true;
        }, IntPtr.Zero);
        
        if (foundHwnd == IntPtr.Zero) { Console.WriteLine("NOT_FOUND"); return; }
        SetForegroundWindow(foundHwnd);
        System.Threading.Thread.Sleep(500);
        RECT rect; GetWindowRect(foundHwnd, out rect);
        int w = rect.R - rect.L, h2 = rect.B - rect.T;
        using (var bmp = new Bitmap(w, h2)) {
            using (var g = Graphics.FromImage(bmp)) {
                g.CopyFromScreen(rect.L, rect.T, 0, 0, new Size(w, h2));
            }
            bmp.Save(outPath, ImageFormat.Png);
        }
        Console.WriteLine($"OK {w}x{h2}");
    }
}
