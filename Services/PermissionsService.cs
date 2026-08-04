using SmartSink.Models;
using Windows.ApplicationModel;
using Windows.Devices.Bluetooth;
using Windows.Foundation.Metadata;
using Windows.System;

namespace SmartSink.Services;

public sealed class PermissionsService
{
    private readonly LogService _log;

    public PermissionsService(LogService log) => _log = log;

    public async Task<PermissionSnapshot> CheckBluetoothAccessAsync()
    {
        if (!ApiInformation.IsTypePresent("Windows.Devices.Bluetooth.BluetoothAdapter"))
        {
            return new PermissionSnapshot(
                "Unavailable",
                "This Windows version cannot provide Bluetooth adapter information.",
                "Manifest: bluetooth · API unavailable",
                false);
        }

        try
        {
            BluetoothAdapter? adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter is null)
            {
                _log.Info("Bluetooth capability is declared, but Windows reported no default adapter.");
                return new PermissionSnapshot(
                    "Declared · adapter unavailable",
                    "Bluetooth access is included in the package, but Windows cannot currently find an enabled adapter.",
                    "Manifest: bluetooth · Adapter: unavailable",
                    false);
            }

            _log.Info("Bluetooth capability and default adapter access verified.");
            return new PermissionSnapshot(
                "Ready",
                "Bluetooth access is declared and Windows can access the default adapter. SmartSink only lists compatible already-paired devices.",
                "Manifest: bluetooth · Adapter: available",
                true);
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.Error("Windows denied Bluetooth adapter access", ex);
            return new PermissionSnapshot(
                "Blocked by Windows",
                "Windows denied Bluetooth adapter access. Review Bluetooth and app settings, then refresh.",
                "Manifest: bluetooth · Access: denied",
                false);
        }
        catch (Exception ex)
        {
            _log.Error("Bluetooth access verification failed", ex);
            return new PermissionSnapshot(
                "Could not verify",
                "Windows could not check the Bluetooth adapter. Receiver errors will still appear on the main screen.",
                $"Manifest: bluetooth · {ex.GetType().Name}",
                false);
        }
    }

    public async Task<bool> OpenAppSettingsAsync()
    {
        try
        {
            string familyName = Package.Current.Id.FamilyName;
            string uri = $"ms-settings:appsfeatures-app?{Uri.EscapeDataString(familyName)}";
            bool opened = await Launcher.LaunchUriAsync(new Uri(uri));
            if (!opened) opened = await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures"));
            return opened;
        }
        catch (Exception ex)
        {
            _log.Error("Opening Windows app settings failed", ex);
            return false;
        }
    }

    public async Task<bool> OpenStartupSettingsAsync()
    {
        try
        {
            return await Launcher.LaunchUriAsync(new Uri("ms-settings:startupapps"));
        }
        catch (Exception ex)
        {
            _log.Error("Opening Windows startup settings failed", ex);
            return false;
        }
    }
}
