using System.Diagnostics;
using Windows.Storage;

namespace SmartSink.Services;

public sealed class LogService
{
    private readonly object _gate = new();
    private readonly string _path;

    public LogService()
    {
        string folder;
        try
        {
            folder = ApplicationData.Current.LocalFolder.Path;
        }
        catch
        {
            folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartSink");
            Directory.CreateDirectory(folder);
        }

        _path = Path.Combine(folder, "SmartSink.log");
    }

    public string LogPath => _path;

    public void Info(string message) => Write("INFO", message);
    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}: {exception}");

    [Conditional("DEBUG")]
    public void Debug(string message) => Write("DEBUG", message);

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}";
        System.Diagnostics.Debug.WriteLine(line);
        try
        {
            lock (_gate)
            {
                File.AppendAllText(_path, line);
            }
        }
        catch
        {
            // Logging must never terminate the receiver.
        }
    }
}
