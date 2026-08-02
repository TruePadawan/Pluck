using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pluck.Cli.Utils;

public static class ClipboardHelper
{
    public static void Copy(string text)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsClipboard.SetText(text);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            ExecuteCommand("pbcopy", text);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Detect Wayland vs X11
            var xdgSession = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
            var isWayland = string.Equals(xdgSession, "wayland", StringComparison.OrdinalIgnoreCase);

            if (isWayland)
            {
                ExecuteCommand("wl-copy", text);
            }
            else
            {
                // Fallback to xsel or xclip for X11 environments
                ExecuteCommand("xsel", text, "--clipboard --input");
            }
        }
    }

    private static void ExecuteCommand(string command, string inputText, string arguments = "")
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                using (var writer = process.StandardInput)
                {
                    writer.Write(inputText);
                }

                process.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to copy via {command}: {ex.Message}");
        }
    }
}

// Minimal, dependency-free Windows implementation using native Win32 APIs
internal static class WindowsClipboard
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    public static void SetText(string text)
    {
        if (!OpenClipboard(IntPtr.Zero)) return;
        try
        {
            EmptyClipboard();
            IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)((text.Length + 1) * 2));
            if (hGlobal == IntPtr.Zero) return;

            IntPtr target = GlobalLock(hGlobal);
            if (target == IntPtr.Zero) return;

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                Marshal.WriteInt16(target, text.Length * 2, 0); // Null terminator
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            SetClipboardData(CF_UNICODETEXT);
        }
        finally
        {
            CloseClipboard();
        }
    }
}