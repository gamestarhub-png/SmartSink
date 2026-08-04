using System.Runtime.InteropServices;

namespace SmartSink.Interop;

internal static class NativeMethods
{
    internal const uint WmApp = 0x8000;
    internal const uint WmNull = 0x0000;
    internal const uint WmUser = 0x0400;
    internal const uint WmContextMenu = 0x007B;
    internal const uint WmLButtonUp = 0x0202;
    internal const uint WmLButtonDoubleClick = 0x0203;
    internal const uint WmRButtonUp = 0x0205;
    internal const uint NinSelect = WmUser;
    internal const uint NinKeySelect = WmUser + 1;

    internal const uint NimAdd = 0;
    internal const uint NimModify = 1;
    internal const uint NimDelete = 2;
    internal const uint NimSetVersion = 4;
    internal const uint NotifyIconVersion4 = 4;
    internal const uint NifMessage = 0x0001;
    internal const uint NifIcon = 0x0002;
    internal const uint NifTip = 0x0004;
    internal const uint NifShowTip = 0x0080;

    internal const uint MfString = 0x0000;
    internal const uint MfSeparator = 0x0800;
    internal const uint MfPopup = 0x0010;
    internal const uint MfChecked = 0x0008;
    internal const uint MfGrayed = 0x0001;
    internal const uint TpmRightButton = 0x0002;
    internal const uint TpmReturnCommand = 0x0100;

    internal const int SwRestore = 9;
    internal const int SmCxSmallIcon = 49;
    internal const int SmCySmallIcon = 50;
    internal const uint ImageIcon = 1;
    internal const uint LrLoadFromFile = 0x0010;
    internal const uint LrDefaultSize = 0x0040;
    internal static readonly nint IdiApplication = new(32512);

    internal delegate nint SubclassProc(nint hWnd, uint message, nuint wParam, nint lParam, nuint subclassId, nuint referenceData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint RegisterWindowMessageW(string message);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowSubclass(nint hWnd, SubclassProc callback, nuint subclassId, nuint referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RemoveWindowSubclass(nint hWnd, SubclassProc callback, nuint subclassId);

    [DllImport("comctl32.dll")]
    internal static extern nint DefSubclassProc(nint hWnd, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AppendMenuW(nint menu, uint flags, nuint item, string? text);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyMenu(nint menu);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hWnd, nint parameters);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessageW(nint hWnd, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint hWnd, int command);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint LoadIconW(nint instance, nint iconName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadImageW", SetLastError = true)]
    internal static extern nint LoadImageW(nint instance, string imageName, uint imageType, int width, int height, uint loadFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(nint icon);
}
