using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmartSink.Models;

public sealed class AppSettings : INotifyPropertyChanged
{
    private bool _startMinimized = true;
    private bool _closeToTray = true;
    private bool _autoEnableReceiver;
    private bool _autoReconnect = true;
    private string? _lastDeviceId;

    public bool StartMinimized { get => _startMinimized; set => Set(ref _startMinimized, value); }
    public bool CloseToTray { get => _closeToTray; set => Set(ref _closeToTray, value); }
    public bool AutoEnableReceiver { get => _autoEnableReceiver; set => Set(ref _autoEnableReceiver, value); }
    public bool AutoReconnect { get => _autoReconnect; set => Set(ref _autoReconnect, value); }
    public string? LastDeviceId { get => _lastDeviceId; set => Set(ref _lastDeviceId, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
