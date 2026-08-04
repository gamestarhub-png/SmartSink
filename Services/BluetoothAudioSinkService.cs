using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using SmartSink.Models;
using SmartSink.ViewModels;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Media.Audio;

namespace SmartSink.Services;

public sealed class BluetoothAudioSinkService : IAsyncDisposable
{
    private sealed record ConnectionRegistration(
        AudioPlaybackConnection Connection,
        TypedEventHandler<AudioPlaybackConnection, object> StateChangedHandler);

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly LogService _log;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly Dictionary<string, ConnectionRegistration> _connections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DeviceInformation> _deviceInformation = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _shutdown = new();
    private DeviceWatcher? _watcher;
    private bool _isShuttingDown;
    private string? _selectedDeviceId;
    private ReceiverState _applicationState = ReceiverState.Discovering;

    public BluetoothAudioSinkService(
        DispatcherQueue dispatcherQueue,
        SettingsService settingsService,
        AppSettings settings,
        LogService log)
    {
        _dispatcherQueue = dispatcherQueue;
        _settingsService = settingsService;
        _settings = settings;
        _log = log;
        _selectedDeviceId = settings.LastDeviceId;

        ApiAvailable = ApiInformation.IsTypePresent("Windows.Media.Audio.AudioPlaybackConnection");
        AvailabilityMessage = ApiAvailable
            ? string.Empty
            : "Bluetooth audio reception requires Windows 10 build 19041 or newer and compatible Bluetooth hardware and drivers.";
    }

    public ObservableCollection<BluetoothDeviceViewModel> Devices { get; } = [];
    public bool ApiAvailable { get; }
    public string AvailabilityMessage { get; }
    public string? SelectedDeviceId => _selectedDeviceId;
    public ReceiverState ApplicationState => _applicationState;
    public bool IsAnyEnabled => Devices.Any(device => device.IsEnabled);
    public string DiscoveryStatus => _watcher?.Status.ToString() ?? (ApiAvailable ? "Not started" : "API unavailable");
    public int EnabledConnectionCount => Devices.Count(device => device.IsEnabled);

    public event EventHandler? StateUpdated;
    public event EventHandler? EnumerationCompleted;

    public void StartDiscovery()
    {
        if (!ApiAvailable || _watcher is not null || _isShuttingDown)
        {
            if (!ApiAvailable)
            {
                SetApplicationState(ReceiverState.Error);
            }
            return;
        }

        try
        {
            string selector = AudioPlaybackConnection.GetDeviceSelector();
            var watcher = DeviceInformation.CreateWatcher(selector);
            watcher.Added += Watcher_Added;
            watcher.Updated += Watcher_Updated;
            watcher.Removed += Watcher_Removed;
            watcher.EnumerationCompleted += Watcher_EnumerationCompleted;
            watcher.Stopped += Watcher_Stopped;
            _watcher = watcher;
            SetApplicationState(ReceiverState.Discovering);
            watcher.Start();
            _log.Info("Bluetooth audio device watcher started.");
        }
        catch (Exception ex)
        {
            _log.Error("Could not start Bluetooth audio device discovery", ex);
            SetApplicationState(ReceiverState.Error);
            _ = RunOnUIAsync(() => StateUpdated?.Invoke(this, EventArgs.Empty));
        }
    }

    public void SelectDevice(string? deviceId)
    {
        _selectedDeviceId = deviceId;
        RaiseStateUpdated();
    }

    public async Task EnableAsync(string deviceId, bool openImmediately = true)
    {
        if (!ApiAvailable || string.IsNullOrWhiteSpace(deviceId) || _isShuttingDown)
        {
            return;
        }

        await _operationLock.WaitAsync(_shutdown.Token).ConfigureAwait(false);
        try
        {
            if (_connections.ContainsKey(deviceId))
            {
                return;
            }

            await UpdateDeviceAsync(deviceId, device =>
            {
                device.IsEnabled = false;
                device.IsConnected = false;
                device.ErrorMessage = null;
                device.State = ReceiverState.Enabling;
            }).ConfigureAwait(false);

            AudioPlaybackConnection? connection = AudioPlaybackConnection.TryCreateFromId(deviceId);
            if (connection is null)
            {
                await SetDeviceErrorAsync(deviceId, "This device is unavailable or does not support Bluetooth audio streaming to Windows.").ConfigureAwait(false);
                _log.Error("AudioPlaybackConnection could not be created for the selected device.");
                return;
            }

            TypedEventHandler<AudioPlaybackConnection, object> handler = (_, _) => Connection_StateChanged(deviceId, connection);
            connection.StateChanged += handler;
            _connections[deviceId] = new ConnectionRegistration(connection, handler);

            try
            {
                _log.Info("Enabling Bluetooth audio reception.");
                await connection.StartAsync().AsTask().ConfigureAwait(false);
                _log.Info("AudioPlaybackConnection.StartAsync completed.");
                _settings.LastDeviceId = deviceId;
                _settingsService.Save(_settings);
                _selectedDeviceId = deviceId;

                await UpdateDeviceAsync(deviceId, device =>
                {
                    device.IsEnabled = true;
                    device.IsConnected = connection.State == AudioPlaybackConnectionState.Opened;
                    device.State = device.IsConnected ? ReceiverState.Connected : ReceiverState.Waiting;
                    device.ErrorMessage = null;
                }).ConfigureAwait(false);

                if (openImmediately && connection.State != AudioPlaybackConnectionState.Opened)
                {
                    await OpenCoreAsync(deviceId, connection).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                ReleaseConnection(deviceId);
                await SetDeviceErrorAsync(deviceId, FriendlyConnectionError(ex)).ConfigureAwait(false);
                _log.Error("Enabling Bluetooth audio reception failed", ex);
            }
        }
        catch (OperationCanceledException) when (_isShuttingDown)
        {
        }
        finally
        {
            _operationLock.Release();
            RaiseStateUpdated();
        }
    }

    public async Task ReconnectAsync(string deviceId)
    {
        if (_isShuttingDown) return;

        await _operationLock.WaitAsync(_shutdown.Token).ConfigureAwait(false);
        try
        {
            if (_connections.TryGetValue(deviceId, out var registration))
            {
                if (registration.Connection.State != AudioPlaybackConnectionState.Opened)
                {
                    await OpenCoreAsync(deviceId, registration.Connection).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (_isShuttingDown)
        {
        }
        finally
        {
            _operationLock.Release();
            RaiseStateUpdated();
        }
    }

    public async Task DisableAsync(string deviceId)
    {
        await _operationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisableCoreAsync(deviceId).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
            RaiseStateUpdated();
        }
    }

    public async Task DisableAllAsync()
    {
        await _operationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (string deviceId in _connections.Keys.ToArray())
            {
                await DisableCoreAsync(deviceId).ConfigureAwait(false);
            }
        }
        finally
        {
            _operationLock.Release();
            RaiseStateUpdated();
        }
    }

    public async Task ShutdownAsync()
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;
        _shutdown.Cancel();

        var watcher = _watcher;
        _watcher = null;
        if (watcher is not null)
        {
            watcher.Added -= Watcher_Added;
            watcher.Updated -= Watcher_Updated;
            watcher.Removed -= Watcher_Removed;
            watcher.EnumerationCompleted -= Watcher_EnumerationCompleted;
            watcher.Stopped -= Watcher_Stopped;
            try
            {
                if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                {
                    watcher.Stop();
                }
            }
            catch (Exception ex)
            {
                _log.Error("Stopping the Bluetooth device watcher failed", ex);
            }
        }

        await _operationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (string deviceId in _connections.Keys.ToArray())
            {
                ReleaseConnection(deviceId);
            }
        }
        finally
        {
            _operationLock.Release();
        }

        _log.Info("Bluetooth audio service shut down and all connections were released.");
    }

    public ValueTask DisposeAsync() => new(ShutdownAsync());

    private async Task OpenCoreAsync(string deviceId, AudioPlaybackConnection connection)
    {
        await UpdateDeviceAsync(deviceId, device =>
        {
            device.State = ReceiverState.Connecting;
            device.ErrorMessage = null;
        }).ConfigureAwait(false);

        try
        {
            var result = await connection.OpenAsync().AsTask().ConfigureAwait(false);
            _log.Info($"AudioPlaybackConnection.OpenAsync completed with status {result.Status}.");
            if (result.Status == AudioPlaybackConnectionOpenResultStatus.Success)
            {
                await UpdateDeviceAsync(deviceId, device =>
                {
                    device.IsEnabled = true;
                    device.IsConnected = true;
                    device.State = ReceiverState.Connected;
                    device.ErrorMessage = null;
                }).ConfigureAwait(false);
            }
            else
            {
                string detail = result.ExtendedError is null ? string.Empty : $" ({result.ExtendedError.Message})";
                await SetDeviceErrorAsync(deviceId, $"Windows could not connect to the phone: {result.Status}{detail}").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await SetDeviceErrorAsync(deviceId, FriendlyConnectionError(ex)).ConfigureAwait(false);
            _log.Error("Opening the Bluetooth audio connection failed", ex);
        }
    }

    private async Task DisableCoreAsync(string deviceId)
    {
        await UpdateDeviceAsync(deviceId, device => device.State = ReceiverState.Disconnecting).ConfigureAwait(false);
        ReleaseConnection(deviceId);
        await UpdateDeviceAsync(deviceId, device =>
        {
            device.IsEnabled = false;
            device.IsConnected = false;
            device.State = ReceiverState.Disabled;
            device.ErrorMessage = null;
        }).ConfigureAwait(false);
        _log.Info("Bluetooth audio reception disabled; the app-owned connection was released.");
    }

    private void ReleaseConnection(string deviceId)
    {
        if (!_connections.Remove(deviceId, out var registration)) return;
        registration.Connection.StateChanged -= registration.StateChangedHandler;
        registration.Connection.Dispose();
    }

    private void Connection_StateChanged(string deviceId, AudioPlaybackConnection connection)
    {
        _log.Info($"Bluetooth audio connection state changed to {connection.State}.");
        _ = UpdateDeviceAsync(deviceId, device =>
        {
            bool opened = connection.State == AudioPlaybackConnectionState.Opened;
            device.IsEnabled = true;
            device.IsConnected = opened;
            device.State = opened ? ReceiverState.Connected : ReceiverState.Waiting;
            if (opened) device.ErrorMessage = null;
        });

        if (connection.State == AudioPlaybackConnectionState.Closed && _settings.AutoReconnect && !_isShuttingDown)
        {
            _ = AttemptAutomaticReconnectAsync(deviceId, connection);
        }
    }

    private async Task AttemptAutomaticReconnectAsync(string deviceId, AudioPlaybackConnection expectedConnection)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), _shutdown.Token).ConfigureAwait(false);
            await _operationLock.WaitAsync(_shutdown.Token).ConfigureAwait(false);
            try
            {
                if (_connections.TryGetValue(deviceId, out var registration) &&
                    ReferenceEquals(registration.Connection, expectedConnection) &&
                    registration.Connection.State == AudioPlaybackConnectionState.Closed)
                {
                    await OpenCoreAsync(deviceId, registration.Connection).ConfigureAwait(false);
                }
            }
            finally
            {
                _operationLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Watcher_Added(DeviceWatcher sender, DeviceInformation device)
    {
        _log.Info($"Compatible Bluetooth audio device discovered: {device.Name}.");
        _log.Debug($"Discovered device ID: {device.Id}");
        lock (_deviceInformation)
        {
            _deviceInformation[device.Id] = device;
        }

        _ = RunOnUIAsync(() =>
        {
            var existing = Devices.FirstOrDefault(item => item.Id == device.Id);
            if (existing is null)
            {
                Devices.Add(new BluetoothDeviceViewModel(device.Id, device.Name));
            }
            else
            {
                existing.Name = device.Name;
                existing.IsAvailable = true;
                if (!existing.IsEnabled) existing.State = ReceiverState.Available;
            }
            RaiseStateUpdatedOnUI();
        });
    }

    private void Watcher_Updated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        DeviceInformation? information;
        lock (_deviceInformation)
        {
            _deviceInformation.TryGetValue(update.Id, out information);
            information?.Update(update);
        }

        if (information is not null)
        {
            _ = UpdateDeviceAsync(update.Id, device =>
            {
                device.Name = information.Name;
                device.IsAvailable = information.IsEnabled;
            });
        }
    }

    private void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        _log.Info("A compatible Bluetooth audio device was removed or unpaired.");
        _log.Debug($"Removed device ID: {update.Id}");
        lock (_deviceInformation)
        {
            _deviceInformation.Remove(update.Id);
        }
        _ = HandleRemovedAsync(update.Id);
    }

    private void Watcher_EnumerationCompleted(DeviceWatcher sender, object args)
    {
        _log.Info("Bluetooth audio device enumeration completed.");
        _ = RunOnUIAsync(() =>
        {
            if (Devices.Count == 0 && !_connections.Any()) SetApplicationStateOnUI(ReceiverState.Available);
            EnumerationCompleted?.Invoke(this, EventArgs.Empty);
            RaiseStateUpdatedOnUI();
        });
    }

    private void Watcher_Stopped(DeviceWatcher sender, object args)
    {
        _log.Info($"Bluetooth audio device watcher stopped with status {sender.Status}.");
        if (!_isShuttingDown)
        {
            _ = RunOnUIAsync(() =>
            {
                foreach (var device in Devices)
                {
                    device.IsAvailable = false;
                    if (!device.IsConnected)
                    {
                        device.State = ReceiverState.Error;
                        device.ErrorMessage = "Bluetooth device discovery stopped. Check that Bluetooth is turned on.";
                    }
                }
                SetApplicationStateOnUI(ReceiverState.Error);
            });
        }
    }

    private async Task HandleRemovedAsync(string deviceId)
    {
        await DisableAsync(deviceId).ConfigureAwait(false);
        await RunOnUIAsync(() =>
        {
            var device = Devices.FirstOrDefault(item => item.Id == deviceId);
            if (device is not null) Devices.Remove(device);
            if (_selectedDeviceId == deviceId) _selectedDeviceId = null;
            RaiseStateUpdatedOnUI();
        }).ConfigureAwait(false);
    }

    private Task SetDeviceErrorAsync(string deviceId, string message) => UpdateDeviceAsync(deviceId, device =>
    {
        device.IsConnected = false;
        device.State = ReceiverState.Error;
        device.ErrorMessage = message;
    });

    private Task UpdateDeviceAsync(string deviceId, Action<BluetoothDeviceViewModel> update) => RunOnUIAsync(() =>
    {
        var device = Devices.FirstOrDefault(item => item.Id == deviceId);
        if (device is not null) update(device);
        RecalculateApplicationStateOnUI();
        RaiseStateUpdatedOnUI();
    });

    private void RaiseStateUpdated() => _ = RunOnUIAsync(RaiseStateUpdatedOnUI);

    private void RaiseStateUpdatedOnUI()
    {
        RecalculateApplicationStateOnUI();
        StateUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void RecalculateApplicationStateOnUI()
    {
        var selected = Devices.FirstOrDefault(device => device.Id == _selectedDeviceId);
        var active = selected ?? Devices.FirstOrDefault(device => device.IsEnabled);
        _applicationState = active?.State ?? (_watcher?.Status == DeviceWatcherStatus.Started
            ? ReceiverState.Discovering
            : ReceiverState.Available);
    }

    private void SetApplicationState(ReceiverState state) => _ = RunOnUIAsync(() => SetApplicationStateOnUI(state));

    private void SetApplicationStateOnUI(ReceiverState state)
    {
        _applicationState = state;
        StateUpdated?.Invoke(this, EventArgs.Empty);
    }

    private Task RunOnUIAsync(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }))
        {
            completion.SetException(new InvalidOperationException("The UI dispatcher is unavailable."));
        }
        return completion.Task;
    }

    private static string FriendlyConnectionError(Exception exception) => exception.HResult switch
    {
        unchecked((int)0x8007048F) => "Bluetooth is turned off or the phone is no longer available.",
        unchecked((int)0x80070490) => "The phone is no longer paired or available.",
        _ => "Windows could not enable Bluetooth audio reception. Check Bluetooth, the phone, and the adapter driver."
    };
}
