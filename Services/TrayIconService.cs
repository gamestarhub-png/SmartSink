using System.Runtime.InteropServices;
using SmartSink.Interop;
using SmartSink.Models;
using SmartSink.ViewModels;

namespace SmartSink.Services;

public sealed class TrayIconService : IDisposable
{
    private const uint IconId = 1;
    private const uint CallbackMessage = NativeMethods.WmApp + 42;
    private const nuint SubclassId = 0x534D4152;
    private const uint CommandOpen = 100;
    private const uint CommandAccept = 101;
    private const uint CommandReconnect = 102;
    private const uint CommandExit = 104;
    private const uint FirstDeviceCommand = 1000;

    private readonly nint _windowHandle;
    private readonly BluetoothAudioSinkService _sink;
    private readonly LogService _log;
    private readonly NativeMethods.SubclassProc _subclassProc;
    private readonly uint _taskbarCreatedMessage;
    private nint _icon;
    private bool _ownsIcon;
    private bool _added;
    private bool _disposed;

    public TrayIconService(nint windowHandle, BluetoothAudioSinkService sink, LogService log)
    {
        _windowHandle = windowHandle;
        _sink = sink;
        _log = log;
        _subclassProc = WindowSubclassProc;
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessageW("TaskbarCreated");
    }

    public event Action? OpenRequested;
    public event Action? ToggleReceiverRequested;
    public event Action? ReconnectRequested;
    public event Action? ExitRequested;
    public event Action<string>? DeviceRequested;

    public void Initialize()
    {
        if (_disposed || _added) return;
        if (!NativeMethods.SetWindowSubclass(_windowHandle, _subclassProc, SubclassId, 0))
        {
            throw new InvalidOperationException($"Could not attach the tray callback (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        string trayIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "TrayIcon.ico");
        int trayIconWidth = NativeMethods.GetSystemMetrics(NativeMethods.SmCxSmallIcon);
        int trayIconHeight = NativeMethods.GetSystemMetrics(NativeMethods.SmCySmallIcon);
        _icon = NativeMethods.LoadImageW(
            0,
            trayIconPath,
            NativeMethods.ImageIcon,
            trayIconWidth,
            trayIconHeight,
            NativeMethods.LrLoadFromFile);
        _ownsIcon = _icon != 0;
        if (_icon == 0)
        {
            _icon = NativeMethods.LoadIconW(0, NativeMethods.IdiApplication);
            _log.Error($"Could not load the custom tray icon (Win32 error {Marshal.GetLastWin32Error()}); using the Windows fallback icon.");
        }
        AddIcon();
        _log.Info("Notification-area icon initialized.");
    }

    public void UpdateStatus()
    {
        if (!_added || _disposed) return;
        var data = CreateData();
        data.uFlags = NativeMethods.NifIcon | NativeMethods.NifTip | NativeMethods.NifShowTip;
        data.szTip = BuildTooltip();
        NativeMethods.Shell_NotifyIconW(NativeMethods.NimModify, ref data);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_added)
        {
            var data = CreateData();
            NativeMethods.Shell_NotifyIconW(NativeMethods.NimDelete, ref data);
            _added = false;
        }

        NativeMethods.RemoveWindowSubclass(_windowHandle, _subclassProc, SubclassId);
        if (_ownsIcon && _icon != 0)
        {
            NativeMethods.DestroyIcon(_icon);
        }
        _icon = 0;
        _ownsIcon = false;
        GC.SuppressFinalize(this);
    }

    private void AddIcon()
    {
        var data = CreateData();
        data.uFlags = NativeMethods.NifMessage | NativeMethods.NifIcon | NativeMethods.NifTip | NativeMethods.NifShowTip;
        data.uCallbackMessage = CallbackMessage;
        data.szTip = BuildTooltip();
        if (!NativeMethods.Shell_NotifyIconW(NativeMethods.NimAdd, ref data))
        {
            throw new InvalidOperationException($"Shell_NotifyIcon failed (Win32 error {Marshal.GetLastWin32Error()}).");
        }

        data.uTimeoutOrVersion = NativeMethods.NotifyIconVersion4;
        NativeMethods.Shell_NotifyIconW(NativeMethods.NimSetVersion, ref data);
        _added = true;
    }

    private NotifyIconData CreateData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
        hWnd = _windowHandle,
        uID = IconId,
        hIcon = _icon,
        szTip = string.Empty,
        szInfo = string.Empty,
        szInfoTitle = string.Empty
    };

    private string BuildTooltip()
    {
        var selected = _sink.Devices.FirstOrDefault(device => device.Id == _sink.SelectedDeviceId)
            ?? _sink.Devices.FirstOrDefault(device => device.IsEnabled);
        string suffix = _sink.ApplicationState switch
        {
            ReceiverState.Connected => $"Connected to {selected?.Name ?? "phone"}",
            ReceiverState.Waiting or ReceiverState.Connecting or ReceiverState.Enabling => $"Waiting for {selected?.Name ?? "phone"}",
            ReceiverState.Error => "Connection error",
            _ => "Receiver disabled"
        };
        string result = $"SmartSink: {suffix}";
        return result.Length < 128 ? result : result[..127];
    }

    private nint WindowSubclassProc(nint hWnd, uint message, nuint wParam, nint lParam, nuint subclassId, nuint referenceData)
    {
        if (message == _taskbarCreatedMessage)
        {
            _added = false;
            try
            {
                AddIcon();
                _log.Info("Explorer restarted; the notification-area icon was restored.");
            }
            catch (Exception ex)
            {
                _log.Error("Could not restore the notification-area icon after Explorer restarted", ex);
            }
            return 0;
        }

        if (message == CallbackMessage)
        {
            uint notification = unchecked((uint)lParam.ToInt64()) & 0xFFFF;
            switch (notification)
            {
                case NativeMethods.NinSelect:
                case NativeMethods.NinKeySelect:
                case NativeMethods.WmLButtonUp:
                case NativeMethods.WmLButtonDoubleClick:
                    OpenRequested?.Invoke();
                    break;
                case NativeMethods.WmContextMenu:
                case NativeMethods.WmRButtonUp:
                    ShowContextMenu();
                    break;
            }
            return 0;
        }

        return NativeMethods.DefSubclassProc(hWnd, message, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        nint menu = NativeMethods.CreatePopupMenu();
        nint devicesMenu = NativeMethods.CreatePopupMenu();
        if (menu == 0 || devicesMenu == 0)
        {
            if (menu != 0) NativeMethods.DestroyMenu(menu);
            if (devicesMenu != 0) NativeMethods.DestroyMenu(devicesMenu);
            return;
        }

        var devices = _sink.Devices.ToArray();
        try
        {
            Append(menu, 0, CommandOpen, "Open");
            Append(menu, NativeMethods.MfSeparator, 0, null);
            Append(menu, _sink.IsAnyEnabled ? NativeMethods.MfChecked : 0, CommandAccept, "Accept phone audio");
            Append(menu, devices.Any(device => device.IsEnabled) ? 0 : NativeMethods.MfGrayed, CommandReconnect, "Reconnect");

            if (devices.Length == 0)
            {
                Append(devicesMenu, NativeMethods.MfGrayed, 0, "No compatible devices");
            }
            else
            {
                for (int index = 0; index < devices.Length; index++)
                {
                    uint flags = devices[index].Id == _sink.SelectedDeviceId ? NativeMethods.MfChecked : 0;
                    Append(devicesMenu, flags, FirstDeviceCommand + (uint)index, devices[index].Name);
                }
            }
            Append(menu, NativeMethods.MfPopup, unchecked((nuint)devicesMenu), "Devices");
            Append(menu, NativeMethods.MfSeparator, 0, null);
            Append(menu, 0, CommandExit, "Exit");

            NativeMethods.GetCursorPos(out var point);
            NativeMethods.SetForegroundWindow(_windowHandle);
            uint command = NativeMethods.TrackPopupMenuEx(
                menu,
                NativeMethods.TpmRightButton | NativeMethods.TpmReturnCommand,
                point.X,
                point.Y,
                _windowHandle,
                0);
            NativeMethods.PostMessageW(_windowHandle, NativeMethods.WmNull, 0, 0);

            switch (command)
            {
                case CommandOpen: OpenRequested?.Invoke(); break;
                case CommandAccept: ToggleReceiverRequested?.Invoke(); break;
                case CommandReconnect: ReconnectRequested?.Invoke(); break;
                case CommandExit: ExitRequested?.Invoke(); break;
                default:
                    if (command >= FirstDeviceCommand && command < FirstDeviceCommand + devices.Length)
                    {
                        DeviceRequested?.Invoke(devices[(int)(command - FirstDeviceCommand)].Id);
                    }
                    break;
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    private static void Append(nint menu, uint flags, nuint id, string? text) =>
        NativeMethods.AppendMenuW(menu, flags, id, text);
}
