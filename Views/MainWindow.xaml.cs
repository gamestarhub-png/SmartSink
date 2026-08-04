using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SmartSink.Interop;
using SmartSink.ViewModels;

namespace SmartSink.Views;

public sealed partial class MainWindow : Window
{
    private bool _isExplicitExit;
    private bool _initialized;

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        SystemBackdrop = new DesktopAcrylicBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        AppWindow.Resize(new Windows.Graphics.SizeInt32(920, 760));
        AppWindow.Closing += AppWindow_Closing;
        Activated += MainWindow_Activated;
        TopNavigation.SelectedItem = ReceiverNavigationItem;
    }

    public MainViewModel ViewModel { get; }

    public void ShowAndActivate()
    {
        nint handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
        AppWindow.Show();
        Activate();
    }

    public void Hide() => AppWindow.Hide();

    public void MarkExplicitExit() => _isExplicitExit = true;

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated) return;
        bool refreshStartup = _initialized;
        _initialized = true;
        if (refreshStartup) await ViewModel.RefreshStartupStatusAsync();
    }

    private async void TopNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        string destination = args.SelectedItemContainer?.Tag?.ToString() ?? "receiver";
        ReceiverPage.Visibility = destination == "receiver" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = destination == "settings" ? Visibility.Visible : Visibility.Collapsed;
        InformationPage.Visibility = destination == "information" ? Visibility.Visible : Visibility.Collapsed;
        if (destination == "settings")
        {
            await ViewModel.RefreshPermissionsAsync();
            await ViewModel.RefreshStartupStatusAsync();
        }
        if (destination == "information") await ViewModel.RefreshDiagnosticsAsync();
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_isExplicitExit && ViewModel.Settings.CloseToTray)
        {
            args.Cancel = true;
            AppWindow.Hide();
        }
    }

    private async void AcceptPhoneAudio_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized || sender is not ToggleSwitch toggle || toggle.IsOn == ViewModel.AcceptPhoneAudio) return;
        toggle.IsEnabled = false;
        try
        {
            await ViewModel.ToggleReceiverAsync(toggle.IsOn);
        }
        finally
        {
            toggle.IsEnabled = ViewModel.ReceiverControlsEnabled;
        }
    }

    private async void RouteDevice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id }) await ViewModel.RouteDeviceAsync(id);
    }

    private async void DisableDevice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id }) await ViewModel.DisableDeviceAsync(id);
    }

    private async void OpenBluetoothSettings_Click(object sender, RoutedEventArgs e) => await ViewModel.OpenBluetoothSettingsAsync();

    private void Setting_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initialized) ViewModel.SaveSettings();
    }

    private async void RefreshDiagnostics_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshDiagnosticsAsync();

    private void CopyStatus_Click(object sender, RoutedEventArgs e) => ViewModel.CopyStatus();

    private async void AuthorWebsite_Click(object sender, RoutedEventArgs e) => await ViewModel.OpenAuthorWebsiteAsync();

    private async void GitHub_Click(object sender, RoutedEventArgs e) => await ViewModel.OpenGitHubAsync();

    private async void RefreshPermissions_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshPermissionsAsync();

    private async void OpenAppPermissionSettings_Click(object sender, RoutedEventArgs e) => await ViewModel.OpenAppPermissionSettingsAsync();

    private async void OpenStartupAppsSettings_Click(object sender, RoutedEventArgs e) => await ViewModel.OpenStartupAppsSettingsAsync();
}
