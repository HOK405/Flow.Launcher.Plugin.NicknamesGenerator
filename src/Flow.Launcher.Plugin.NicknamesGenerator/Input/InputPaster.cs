using System;
using System.Runtime.InteropServices;

namespace Flow.Launcher.Plugin.NicknamesGenerator.Input;

public static class InputPaster
{
    private const int InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;

    private const byte VkControl = 0x11;
    private const byte VkV = 0x56;

    public static void Paste()
    {
        if (!TrySendInputPaste())
            FallbackKeybdEventPaste();
    }

    private static bool TrySendInputPaste()
    {
        INPUT[] inputs = new INPUT[4];

        inputs[0] = CreateKeyInput(VkControl, false);
        inputs[1] = CreateKeyInput(VkV, false);
        inputs[2] = CreateKeyInput(VkV, true);
        inputs[3] = CreateKeyInput(VkControl, true);

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        return sent == inputs.Length;
    }

    private static void FallbackKeybdEventPaste()
    {
        keybd_event(VkControl, 0, 0, UIntPtr.Zero);
        keybd_event(VkV, 0, 0, UIntPtr.Zero);
        keybd_event(VkV, 0, KeyeventfKeyup, UIntPtr.Zero);
        keybd_event(VkControl, 0, KeyeventfKeyup, UIntPtr.Zero);
    }

    private static INPUT CreateKeyInput(byte vk, bool keyUp)
    {
        return new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = keyUp ? KeyeventfKeyup : 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public byte wVk;
        public byte wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
