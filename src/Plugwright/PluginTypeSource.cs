namespace Plugwright;

/// <summary>
/// A plugin type added in code with <see cref="PluginCollection.Add{TPlugin}()"/> or <see cref="PluginCollection.Add(Type)"/>.
/// </summary>
public sealed class PluginTypeSource : PluginSource
{
    internal PluginTypeSource(Type pluginType)
    {
        PluginType = pluginType;
    }

    /// <summary>
    /// Gets the plugin type.
    /// </summary>
    public Type PluginType { get; }

    /// <summary>
    /// Returns a description of the source, for example <c>Type Heartbeat.HeartbeatPlugin</c>.
    /// </summary>
    /// <returns>A description of the source.</returns>
    public override string ToString() => $"Type {PluginType.FullName}";
}
