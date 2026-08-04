namespace SmartSink.Models;

public sealed record BluetoothDiagnostics(
    string AdapterName,
    string AdapterSupport,
    string StatusMessage,
    DateTimeOffset RefreshedAt);
