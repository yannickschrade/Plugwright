namespace Heartbeat;

// --8<-- [start:options]
public sealed class HeartbeatOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);
}
// --8<-- [end:options]
