using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using SmartSink.Models;
using SmartSink.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace SmartSink.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly BluetoothAudioSinkService _sink;
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly PermissionsService _permissionsService;
    private readonly LogService _log;
    private BluetoothDeviceViewModel? _selectedDevice;
    private string _startupMessage = string.Empty;
    private string _adapterName = "Loading…";
    private string _adapterSupport = "Loading…";
    private string _diagnosticsStatus = "Reading available Bluetooth information…";
    private string _diagnosticsNotice = string.Empty;
    private string _lastDiagnosticsRefresh = "Not refreshed";
    private bool _isRefreshingDiagnostics;
    private string _bluetoothPermissionStatus = "Checking…";
    private string _bluetoothPermissionDetail = "Reading the Windows capability state.";
    private string _bluetoothPermissionTechnicalStatus = "Not checked";
    private string _permissionNotice = string.Empty;
    private bool _isRefreshingPermissions;

    public MainViewModel(
        BluetoothAudioSinkService sink,
        SettingsService settingsService,
        StartupService startupService,
        DiagnosticsService diagnosticsService,
        PermissionsService permissionsService,
        AppSettings settings,
        LogService log)
    {
        _sink = sink;
        _settingsService = settingsService;
        _startupService = startupService;
        _diagnosticsService = diagnosticsService;
        _permissionsService = permissionsService;
        Settings = settings;
        _log = log;
        _sink.StateUpdated += Sink_StateUpdated;
        _sink.EnumerationCompleted += Sink_EnumerationCompleted;
        Devices.CollectionChanged += Devices_CollectionChanged;
        Settings.PropertyChanged += Settings_PropertyChanged;
    }

    public ObservableCollection<BluetoothDeviceViewModel> Devices => _sink.Devices;
    public AppSettings Settings { get; }
    public bool ReceiverControlsEnabled => _sink.ApiAvailable && SelectedDevice is not null;
    public bool IsAvailabilityWarningOpen => !_sink.ApiAvailable;
    public bool AcceptPhoneAudio => SelectedDevice?.IsEnabled == true;
    public Visibility QuickSetupVisibility => Devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DeviceSectionVisibility => Devices.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    public string SelectedPhoneName => SelectedDevice?.Name ?? "No phone selected";
    public string AvailabilityText => _sink.ApiAvailable ? string.Empty : _sink.AvailabilityMessage;
    public string StartupMessage { get => _startupMessage; private set => Set(ref _startupMessage, value); }
    public string AdapterName { get => _adapterName; private set => Set(ref _adapterName, value); }
    public string AdapterSupport { get => _adapterSupport; private set => Set(ref _adapterSupport, value); }
    public string DiagnosticsStatus { get => _diagnosticsStatus; private set => Set(ref _diagnosticsStatus, value); }
    public string DiagnosticsNotice { get => _diagnosticsNotice; private set => Set(ref _diagnosticsNotice, value); }
    public string LastDiagnosticsRefresh { get => _lastDiagnosticsRefresh; private set => Set(ref _lastDiagnosticsRefresh, value); }
    public bool IsRefreshingDiagnostics { get => _isRefreshingDiagnostics; private set { if (Set(ref _isRefreshingDiagnostics, value)) OnPropertyChanged(nameof(CanRefreshDiagnostics)); } }
    public bool CanRefreshDiagnostics => !IsRefreshingDiagnostics;
    public string BluetoothPermissionStatus { get => _bluetoothPermissionStatus; private set => Set(ref _bluetoothPermissionStatus, value); }
    public string BluetoothPermissionDetail { get => _bluetoothPermissionDetail; private set => Set(ref _bluetoothPermissionDetail, value); }
    public string BluetoothPermissionTechnicalStatus { get => _bluetoothPermissionTechnicalStatus; private set => Set(ref _bluetoothPermissionTechnicalStatus, value); }
    public string PermissionNotice { get => _permissionNotice; private set => Set(ref _permissionNotice, value); }
    public bool IsRefreshingPermissions { get => _isRefreshingPermissions; private set { if (Set(ref _isRefreshingPermissions, value)) OnPropertyChanged(nameof(CanRefreshPermissions)); } }
    public bool CanRefreshPermissions => !IsRefreshingPermissions;

    public string AppVersion
    {
        get
        {
            PackageVersion version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }

    public string AudioApiStatus => _sink.ApiAvailable ? "Available" : "Unavailable. Windows 10 build 19041 or newer and a compatible driver are required.";
    public string DeviceWatcherStatus => _sink.DiscoveryStatus;
    public string CompatibleDeviceCount => Devices.Count == 1 ? "1 compatible phone" : $"{Devices.Count} compatible phones";
    public string SelectedDiagnosticPhone => SelectedDevice?.Name ?? "No phone selected";
    public string ReceiverConnectionState => SelectedDevice?.StatusText ?? ApplicationStateText;
    public string AppConnectionOwnership => SelectedDevice?.IsEnabled == true
        ? "SmartSink owns the active route. Stop releases it."
        : "No app-owned A2DP connection";
    public string AudioRouteDescription => SelectedDevice?.IsConnected == true
        ? "Phone → Windows default output (active)"
        : "Windows default output (route not active)";
    public string ConnectionErrorDetail => SelectedDevice?.ErrorMessage ?? "No current connection error";
    public bool IsConnectionErrorOpen => !string.IsNullOrWhiteSpace(SelectedDevice?.ErrorMessage);
    public bool IsBluetoothStatusWarningOpen => AdapterName == "Not available";

    public BluetoothDeviceViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (!Set(ref _selectedDevice, value)) return;
            _sink.SelectDevice(value?.Id);
            RaiseStatusProperties();
        }
    }

    public string ApplicationStateText => _sink.ApplicationState switch
    {
        ReceiverState.Disabled => "Receiver disabled",
        ReceiverState.Discovering => "Looking for compatible paired phones",
        ReceiverState.Available => Devices.Count == 0 ? "No compatible paired phones found" : "Ready",
        ReceiverState.Enabling => "Enabling receiver",
        ReceiverState.Waiting => $"Waiting for {SelectedDevice?.Name ?? "phone"}",
        ReceiverState.Connecting => $"Connecting to {SelectedDevice?.Name ?? "phone"}",
        ReceiverState.Connected => $"Receiving audio from {SelectedDevice?.Name ?? "phone"}",
        ReceiverState.Disconnecting => "Releasing Bluetooth audio connection",
        ReceiverState.Error => SelectedDevice?.ErrorMessage ?? _sink.AvailabilityMessage ?? "Bluetooth sink unavailable",
        _ => "Receiver disabled"
    };

    public async Task InitializeAsync()
    {
        await RefreshStartupStatusAsync();
        SelectPreferredDevice();
        await RefreshPermissionsAsync();
        await RefreshDiagnosticsAsync();
    }

    public async Task ToggleReceiverAsync(bool enabled)
    {
        if (SelectedDevice is null) return;
        if (enabled)
        {
            await _sink.EnableAsync(SelectedDevice.Id, openImmediately: true);
        }
        else
        {
            await _sink.DisableAsync(SelectedDevice.Id);
        }
    }

    public async Task RouteDeviceAsync(string deviceId)
    {
        SelectById(deviceId);
        if (SelectedDevice?.IsEnabled == true)
        {
            await _sink.ReconnectAsync(deviceId);
        }
        else
        {
            await _sink.EnableAsync(deviceId, openImmediately: true);
        }
    }

    public async Task DisableDeviceAsync(string deviceId) => await _sink.DisableAsync(deviceId);

    public async Task ReconnectAsync()
    {
        var device = SelectedDevice ?? Devices.FirstOrDefault(item => item.IsEnabled);
        if (device is null) return;
        if (device.IsEnabled) await _sink.ReconnectAsync(device.Id);
        else await _sink.EnableAsync(device.Id, openImmediately: true);
    }

    public void SelectById(string deviceId) => SelectedDevice = Devices.FirstOrDefault(device => device.Id == deviceId);

    public async Task OpenBluetoothSettingsAsync()
    {
        if (!await Launcher.LaunchUriAsync(new Uri("ms-settings:bluetooth")))
        {
            StartupMessage = "Windows could not open Bluetooth settings.";
        }
    }

    public void SaveSettings() => _settingsService.Save(Settings);

    public async Task RefreshStartupStatusAsync()
    {
        StartupStatus startup = await _startupService.GetStatusAsync();
        StartupMessage = startup.Message;
    }

    public async Task RefreshPermissionsAsync()
    {
        if (IsRefreshingPermissions) return;
        IsRefreshingPermissions = true;
        PermissionNotice = string.Empty;
        try
        {
            ApplyPermissionSnapshot(await _permissionsService.CheckBluetoothAccessAsync());
        }
        finally
        {
            IsRefreshingPermissions = false;
        }
    }

    public async Task OpenAppPermissionSettingsAsync()
    {
        if (!await _permissionsService.OpenAppSettingsAsync())
        {
            PermissionNotice = "Windows could not open SmartSink's app settings.";
        }
    }

    public async Task OpenStartupAppsSettingsAsync()
    {
        if (!await _permissionsService.OpenStartupSettingsAsync())
        {
            PermissionNotice = "Windows could not open Startup Apps settings.";
        }
    }

    public async Task RefreshDiagnosticsAsync()
    {
        if (IsRefreshingDiagnostics) return;
        IsRefreshingDiagnostics = true;
        DiagnosticsNotice = string.Empty;
        try
        {
            BluetoothDiagnostics diagnostics = await _diagnosticsService.GetBluetoothDiagnosticsAsync();
            AdapterName = diagnostics.AdapterName;
            AdapterSupport = diagnostics.AdapterSupport;
            DiagnosticsStatus = diagnostics.StatusMessage;
            LastDiagnosticsRefresh = $"Updated {diagnostics.RefreshedAt:HH:mm:ss}";
        }
        finally
        {
            IsRefreshingDiagnostics = false;
            RaiseDiagnosticProperties();
        }
    }

    public async Task OpenAuthorWebsiteAsync() => await OpenUriAsync("https://xxanqw.pp.ua", "author website");

    public async Task OpenGitHubAsync() => await OpenUriAsync("https://github.com/xxanqw/smartsink", "GitHub repository");

    public void CopyStatus()
    {
        var package = new DataPackage();
        package.SetText(BuildDiagnosticsReport());
        Clipboard.SetContent(package);
        Clipboard.Flush();
        DiagnosticsNotice = "Status copied. No system profile or device identifiers are included.";
    }

    private async void Sink_EnumerationCompleted(object? sender, EventArgs e)
    {
        SelectPreferredDevice();
        if (!Settings.AutoEnableReceiver || string.IsNullOrWhiteSpace(Settings.LastDeviceId)) return;

        var device = Devices.FirstOrDefault(item => item.Id == Settings.LastDeviceId);
        if (device is null) return;
        try
        {
            SelectedDevice = device;
            await _sink.EnableAsync(device.Id, Settings.AutoReconnect);
        }
        catch (Exception ex)
        {
            _log.Error("Automatic receiver enable failed", ex);
        }
    }

    private void Sink_StateUpdated(object? sender, EventArgs e)
    {
        SelectPreferredDevice();
        RaiseStatusProperties();
    }

    private void Devices_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SelectPreferredDevice();
        RaiseStatusProperties();
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _settingsService.Save(Settings);
    }

    private void SelectPreferredDevice()
    {
        if (_selectedDevice is not null && Devices.Contains(_selectedDevice)) return;
        SelectedDevice = Devices.FirstOrDefault(item => item.Id == Settings.LastDeviceId) ?? Devices.FirstOrDefault();
    }

    private void RaiseStatusProperties()
    {
        OnPropertyChanged(nameof(ApplicationStateText));
        OnPropertyChanged(nameof(SelectedPhoneName));
        OnPropertyChanged(nameof(ReceiverControlsEnabled));
        OnPropertyChanged(nameof(AcceptPhoneAudio));
        OnPropertyChanged(nameof(AvailabilityText));
        OnPropertyChanged(nameof(IsAvailabilityWarningOpen));
        OnPropertyChanged(nameof(QuickSetupVisibility));
        OnPropertyChanged(nameof(DeviceSectionVisibility));
        RaiseDiagnosticProperties();
    }

    private void RaiseDiagnosticProperties()
    {
        OnPropertyChanged(nameof(DeviceWatcherStatus));
        OnPropertyChanged(nameof(CompatibleDeviceCount));
        OnPropertyChanged(nameof(SelectedDiagnosticPhone));
        OnPropertyChanged(nameof(ReceiverConnectionState));
        OnPropertyChanged(nameof(AppConnectionOwnership));
        OnPropertyChanged(nameof(AudioRouteDescription));
        OnPropertyChanged(nameof(ConnectionErrorDetail));
        OnPropertyChanged(nameof(IsConnectionErrorOpen));
        OnPropertyChanged(nameof(IsBluetoothStatusWarningOpen));
    }

    private void ApplyPermissionSnapshot(PermissionSnapshot snapshot)
    {
        BluetoothPermissionStatus = snapshot.Status;
        BluetoothPermissionDetail = snapshot.Detail;
        BluetoothPermissionTechnicalStatus = snapshot.TechnicalStatus;
    }

    private async Task OpenUriAsync(string uri, string label)
    {
        if (!await Launcher.LaunchUriAsync(new Uri(uri)))
        {
            DiagnosticsNotice = $"Windows could not open the {label}.";
        }
    }

    private string BuildDiagnosticsReport() => $"""
        SmartSink status
        App version: {AppVersion}
        AudioPlaybackConnection API: {AudioApiStatus}

        Selected phone: {SelectedDiagnosticPhone}
        State: {ReceiverConnectionState}
        Audio route: {AudioRouteDescription}
        Error: {ConnectionErrorDetail}

        Bluetooth: {DiagnosticsStatus}
        Adapter: {AdapterName}
        Support: {AdapterSupport}

        Note: Bluetooth addresses and device identifiers are intentionally omitted.
        """;

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
}
