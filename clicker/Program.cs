using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

[DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
[DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr v);
[DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] i, int size);
[DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);

SetProcessDpiAwarenessContext(new IntPtr(-4)); // PER_MONITOR_AWARE_V2

uint pid = uint.Parse(args[0]);
int relX = int.Parse(args[1]);
int relY = int.Parse(args[2]);

var proc = Process.GetProcessById((int)pid);
var hwnd = proc.MainWindowHandle;
SetForegroundWindow(hwnd);
Thread.Sleep(300);

GetWindowRect(hwnd, out RECT r);
uint dpi = GetDpiForWindow(hwnd);
Console.WriteLine($"Window: {r.L},{r.T} -> {r.R},{r.B} size={r.R-r.L}x{r.B-r.T} DPI={dpi}");

int absX = r.L + relX;
int absY = r.T + relY;
Console.WriteLine($"Clicking at physical: ({absX}, {absY}) relative=({relX}, {relY})");

SetCursorPos(absX, absY);
Thread.Sleep(200);

var inputs = new INPUT[2];
inputs[0].type = 0;
inputs[0].mi.dwFlags = 0x0002;
inputs[1].type = 0;
inputs[1].mi.dwFlags = 0x0004;
SendInput(2, inputs, Marshal.SizeOf<INPUT>());
Console.WriteLine("Click sent");

[StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
[StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public MOUSEINPUT mi; }
[StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
