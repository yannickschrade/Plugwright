namespace Heartbeat;

#region options
public sealed class HeartbeatOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);
}
#endregion
