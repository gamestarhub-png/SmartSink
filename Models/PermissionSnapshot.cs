namespace SmartSink.Models;

public sealed record PermissionSnapshot(
    string Status,
    string Detail,
    string TechnicalStatus,
    bool IsAllowed);
