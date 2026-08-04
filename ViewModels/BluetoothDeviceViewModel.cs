using System.ComponentModel;
using System.Runtime.CompilerServices;
using SmartSink.Models;

namespace SmartSink.ViewModels;

public sealed class BluetoothDeviceViewModel : INotifyPropertyChanged
{
    private string _name;
    private bool _isAvailable = true;
    private bool _isEnabled;
    private bool _isConnected;
    private ReceiverState _state = ReceiverState.Available;
    private string? _errorMessage;

    public BluetoothDeviceViewModel(string id, string name)
    {
        Id = id;
        _name = string.IsNullOrWhiteSpace(name) ? "Bluetooth audio device" : name;
    }

    public string Id { get; }
    public string Name { get => _name; internal set => Set(ref _name, value); }
    public bool IsAvailable
    {
        get => _isAvailable;
        internal set
        {
            if (Set(ref _isAvailable, value)) OnPropertyChanged(nameof(CanRoute));
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        internal set
        {
            if (Set(ref _isEnabled, value)) RaiseActionProperties();
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        internal set
        {
            if (Set(ref _isConnected, value)) RaiseActionProperties();
        }
    }

    public ReceiverState State
    {
        get => _state;
        internal set
        {
            if (Set(ref _state, value))
            {
                OnPropertyChanged(nameof(StatusText));
                RaiseActionProperties();
            }
        }
    }
    public string? ErrorMessage { get => _errorMessage; internal set => Set(ref _errorMessage, value); }

    public bool CanRoute => IsAvailable && !IsConnected && State is
        ReceiverState.Available or ReceiverState.Disabled or ReceiverState.Waiting or ReceiverState.Error;

    public bool CanStop => IsEnabled && State != ReceiverState.Disconnecting;

    public string StatusText => State switch
    {
        ReceiverState.Disabled => "Receiver disabled",
        ReceiverState.Discovering => "Discovering",
        ReceiverState.Available => "Available",
        ReceiverState.Enabling => "Enabling receiver",
        ReceiverState.Waiting => "Waiting for phone",
        ReceiverState.Connecting => "Connecting",
        ReceiverState.Connected => "Receiving audio",
        ReceiverState.Disconnecting => "Disconnecting",
        ReceiverState.Error => ErrorMessage ?? "Connection error",
        _ => State.ToString()
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void RaiseActionProperties()
    {
        OnPropertyChanged(nameof(CanRoute));
        OnPropertyChanged(nameof(CanStop));
    }
}
