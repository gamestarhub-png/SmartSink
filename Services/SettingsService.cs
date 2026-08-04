using SmartSink.Models;
using Windows.Storage;

namespace SmartSink.Services;

public sealed class SettingsService
{
    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

    public AppSettings Load()
    {
        var values = _localSettings.Values;
        return new AppSettings
        {
            StartMinimized = Read(values, nameof(AppSettings.StartMinimized), true),
            CloseToTray = Read(values, nameof(AppSettings.CloseToTray), true),
            AutoEnableReceiver = Read(values, nameof(AppSettings.AutoEnableReceiver), false),
            AutoReconnect = Read(values, nameof(AppSettings.AutoReconnect), true),
            LastDeviceId = values[nameof(AppSettings.LastDeviceId)] as string
        };
    }

    public void Save(AppSettings settings)
    {
        var values = _localSettings.Values;
        values[nameof(AppSettings.StartMinimized)] = settings.StartMinimized;
        values[nameof(AppSettings.CloseToTray)] = settings.CloseToTray;
        values[nameof(AppSettings.AutoEnableReceiver)] = settings.AutoEnableReceiver;
        values[nameof(AppSettings.AutoReconnect)] = settings.AutoReconnect;

        if (string.IsNullOrWhiteSpace(settings.LastDeviceId))
        {
            values.Remove(nameof(AppSettings.LastDeviceId));
        }
        else
        {
            values[nameof(AppSettings.LastDeviceId)] = settings.LastDeviceId;
        }
    }

    private static bool Read(IDictionary<string, object> values, string key, bool fallback) =>
        values.TryGetValue(key, out var value) && value is bool result ? result : fallback;
}
