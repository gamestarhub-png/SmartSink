using SmartSink.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace SmartSink.Services;

public sealed class DiagnosticsService
{
    private readonly LogService _log;

    public DiagnosticsService(LogService log) => _log = log;

    public async Task<BluetoothDiagnostics> GetBluetoothDiagnosticsAsync()
    {
        try
        {
            BluetoothAdapter? adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter is null)
            {
                return Unavailable("Windows did not find an available Bluetooth adapter.");
            }

            DeviceInformation? information = await DeviceInformation.CreateFromIdAsync(adapter.DeviceId);
            string support = adapter.IsClassicSupported
                ? "Bluetooth Classic available · A2DP support depends on the Windows driver"
                : "Bluetooth Classic not reported · A2DP reception may be unavailable";

            return new BluetoothDiagnostics(
                information?.Name ?? "Bluetooth adapter",
                support,
                "Bluetooth is available.",
                DateTimeOffset.Now);
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.Error("Bluetooth status access was denied", ex);
            return Unavailable("Windows denied access to Bluetooth information.");
        }
        catch (Exception ex)
        {
            _log.Error("Loading Bluetooth status failed", ex);
            return Unavailable("Bluetooth information is currently unavailable.");
        }
    }

    private static BluetoothDiagnostics Unavailable(string message) => new(
        "Not available",
        "Not available",
        message,
        DateTimeOffset.Now);
}
