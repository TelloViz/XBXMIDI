using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

public class InputMonitor
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateFile(
        string filename,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    public static void MonitorInput()
    {
        IntPtr deviceHandle = CreateFile(
            @"\\.\DeviceApi",
            0x80000000 | 0x40000000, // GENERIC_READ | GENERIC_WRITE
            0x00000001 | 0x00000002, // FILE_SHARE_READ | FILE_SHARE_WRITE
            IntPtr.Zero,
            3, // OPEN_EXISTING
            0x40000000, // FILE_FLAG_OVERLAPPED
            IntPtr.Zero);

        Debug.WriteLine($"Device Handle: {deviceHandle}");
        Debug.WriteLine($"Last Error: {Marshal.GetLastWin32Error()}");
    }
}