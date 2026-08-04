using Windows.ApplicationModel;

namespace SmartSink.Services;

public sealed record StartupStatus(bool IsAvailable, bool IsEnabled, bool DisabledByUser, string Message);

public sealed class StartupService
{
    public const string TaskId = "BluetoothAudioSinkStartup";
    private readonly LogService _log;

    public StartupService(LogService log) => _log = log;

    public async Task<StartupStatus> GetStatusAsync()
    {
        try
        {
            StartupTask task = await StartupTask.GetAsync(TaskId);
            return Map(task.State);
        }
        catch (Exception ex)
        {
            _log.Error("The packaged startup task is unavailable", ex);
            return new StartupStatus(false, false, false, "Startup control is unavailable for this installation.");
        }
    }

    private static StartupStatus Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled => new(true, true, false, "The app will start with Windows."),
        StartupTaskState.EnabledByPolicy => new(true, true, false, "Startup is enabled by an administrator policy."),
        StartupTaskState.DisabledByUser => new(true, false, true, "Disabled in Windows Startup Apps."),
        StartupTaskState.DisabledByPolicy => new(true, false, false, "Startup is disabled by an administrator policy."),
        StartupTaskState.Disabled => new(true, false, false, "Off in Windows Startup Apps."),
        _ => new(false, false, false, "Startup state is unavailable.")
    };
}
