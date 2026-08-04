namespace SmartSink.Models;

public enum ReceiverState
{
    Disabled,
    Discovering,
    Available,
    Enabling,
    Waiting,
    Connecting,
    Connected,
    Disconnecting,
    Error
}
