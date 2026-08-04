using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using SmartSink.Models;
using SmartSink.Services;
using SmartSink.ViewModels;
using SmartSink.Views;

namespace SmartSink;

public sealed partial class App : Application
{
    private readonly AppActivationArguments _initialActivation;
    private DispatcherQueue? _dispatcherQueue;
    private LogService? _log;
    private SettingsService? _settingsService;
    private AppSettings? _settings;
    private BluetoothAudioSinkService? _sink;
    private StartupService? _startupService;
    private MainViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private WindowService? _windowService;
    private TrayIconService? _tray;
    private bool _initialized;
    private bool _exiting;

    public App() : this(AppInstance.GetCurrent().GetActivatedEventArgs())
    {
    }

    public App(AppActivationArguments initialActivation)
    {
        _initialActivation = initialActivation;
        InitializeComponent();
        UnhandledException += App_UnhandledException;
        AppInstance.GetCurrent().Activated += Current_Activated;
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            await InitializeAsync();
            HandleActivation(_initialActivation);
        }
        catch (Exception ex)
        {
            _log?.Error("Application initialization failed", ex);
            throw;
        }
    }

    private async Task InitializeAsync()
    {
        if (_initialized) return;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _log = new LogService();
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();
        _startupService = new StartupService(_log);
        var diagnosticsService = new DiagnosticsService(_log);
        var permissionsService = new PermissionsService(_log);
        _sink = new BluetoothAudioSinkService(_dispatcherQueue, _settingsService, _settings, _log);
        _viewModel = new MainViewModel(_sink, _settingsService, _startupService, diagnosticsService, permissionsService, _settings, _log);
        _mainWindow = new MainWindow(_viewModel);
        _windowService = new WindowService(_mainWindow);

        nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
        _tray = new TrayIconService(windowHandle, _sink, _log);
        _tray.OpenRequested += ShowMainWindow;
        _tray.ToggleReceiverRequested += Tray_ToggleReceiverRequested;
        _tray.ReconnectRequested += Tray_ReconnectRequested;
        _tray.DeviceRequested += Tray_DeviceRequested;
        _tray.ExitRequested += Tray_ExitRequested;
        _sink.StateUpdated += Sink_StateUpdated;

        _tray.Initialize();
        _sink.StartDiscovery();
        await _viewModel.InitializeAsync();
        _initialized = true;
        _log.Info($"Application initialized. Activation kind: {_initialActivation.Kind}.");
    }

    private void Current_Activated(object? sender, AppActivationArguments args)
    {
        _dispatcherQueue?.TryEnqueue(() => HandleActivation(args));
    }

    private void HandleActivation(AppActivationArguments args)
    {
        _log?.Info($"Activation received: {args.Kind}.");
        if (args.Kind != ExtendedActivationKind.StartupTask || _settings?.StartMinimized == false)
        {
            ShowMainWindow();
        }
    }

    private void ShowMainWindow() => _windowService?.Show();

    private async void Tray_ToggleReceiverRequested()
    {
        if (_viewModel?.SelectedDevice is null)
        {
            ShowMainWindow();
            return;
        }

        await _viewModel.ToggleReceiverAsync(!_viewModel.AcceptPhoneAudio);
    }

    private async void Tray_ReconnectRequested()
    {
        if (_viewModel is not null) await _viewModel.ReconnectAsync();
    }

    private void Tray_DeviceRequested(string deviceId)
    {
        _viewModel?.SelectById(deviceId);
        ShowMainWindow();
    }

    private async void Tray_ExitRequested() => await ExitAsync();

    private void Sink_StateUpdated(object? sender, EventArgs e) => _tray?.UpdateStatus();

    private async Task ExitAsync()
    {
        if (_exiting) return;
        _exiting = true;
        _log?.Info("Explicit application exit requested.");

        try
        {
            if (_sink is not null) await _sink.ShutdownAsync();
        }
        finally
        {
            _tray?.Dispose();
            if (_settings is not null) _settingsService?.Save(_settings);
            _windowService?.CloseForExit();
            Exit();
        }
    }

    private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
    {
        _tray?.Dispose();
        if (_sink is not null && !_exiting)
        {
            try { _sink.ShutdownAsync().GetAwaiter().GetResult(); }
            catch (Exception ex) { _log?.Error("Emergency shutdown cleanup failed", ex); }
        }
        if (_settings is not null) _settingsService?.Save(_settings);
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        _log?.Error("Unhandled application exception", e.Exception);
        e.Handled = true;
        _ = ExitAsync();
    }
}
