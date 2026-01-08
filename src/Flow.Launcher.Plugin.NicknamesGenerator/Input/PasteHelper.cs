using Flow.Launcher.Plugin.NicknamesGenerator.Input;
using System;
using System.Threading;

namespace Flow.Launcher.Plugin.UniqueNamesGenerator.Input;

public static class PasteHelper
{
    private const int ForegroundWaitTimeoutMs = 3000;
    private const int ForegroundPollDelayMs = 15;

    public static IntPtr GetForegroundWindowHandle() => GetForegroundWindow();

    public static void PasteFromClipboard(int initialDelayMs, IntPtr blockedForegroundHandle)
    {
        var thread = new Thread(() =>
        {
            try
            {
                WaitUntilForegroundChanges(initialDelayMs, blockedForegroundHandle);
                InputPaster.Paste();
            }
            catch
            {
            }
        });

        thread.IsBackground = true;
        thread.Start();
    }

    private static void WaitUntilForegroundChanges(int initialDelayMs, IntPtr blockedForegroundHandle)
    {
        Thread.Sleep(Math.Max(0, initialDelayMs));

        if (blockedForegroundHandle == IntPtr.Zero)
            return;

        var start = Environment.TickCount;
        while (Environment.TickCount - start < ForegroundWaitTimeoutMs)
        {
            var fg = GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != blockedForegroundHandle)
                return;
            Thread.Sleep(ForegroundPollDelayMs);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
