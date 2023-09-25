using System;
using System.Runtime.InteropServices;

namespace QuantConnect.Lean.Launcher;

public static class WinNative
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, int ioMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    /// <summary>
    /// This flag enables the user to use the mouse to select and edit text. To enable
    /// this option, you must also set the ExtendedFlags flag.
    /// </summary>
    private const int QuickEditMode = 64;
    private const int InsertMode = 32;
    private const int StdInputHandle = -10;

    // ExtendedFlags must be combined with
    // InsertMode and QuickEditMode when setting
    /// <summary>
    /// ExtendedFlags must be enabled in order to enable InsertMode or QuickEditMode.
    /// </summary>
    private const int ExtendedFlags = 128;

    public static void DisableQuickEdit()
    {
        var conHandle = GetStdHandle(StdInputHandle);
        if (!GetConsoleMode(conHandle, out var mode))
        {
            // error getting the console mode. Exit.
            return;
        }

        mode &= ~(InsertMode | QuickEditMode | ExtendedFlags);

        if (!SetConsoleMode(conHandle, mode))
        {
            // error setting console mode.
        }
    }

    public static void EnableQuickEdit()
    {
        var conHandle = GetStdHandle(StdInputHandle);
        if (!GetConsoleMode(conHandle, out var mode))
        {
            // error getting the console mode. Exit.
            return;
        }

        mode |= (InsertMode | QuickEditMode | ExtendedFlags);

        if (!SetConsoleMode(conHandle, mode))
        {
            // error setting console mode.
        }
    }
}