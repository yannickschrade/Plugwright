namespace Plugwright;

/// <summary>
/// A plugin as seen by plugin handlers: its type, its single instance and its context.
/// </summary>
public sealed class PluginDescriptor
{
    internal PluginDescriptor(Type pluginType, object instance, PluginContext context, string source)
    {
        PluginType = pluginType;
        Instance = instance;
        Context = context;
        Source = source;
    }

    /// <summary>
    /// Gets the plugin type.
    /// </summary>
    public Type PluginType { get; }

    /// <summary>
    /// Gets the plugin instance. The plugin system creates exactly one instance per plugin type,
    /// shared by all handlers in both phases.
    /// </summary>
    public object Instance { get; }

    /// <summary>
    /// Gets the plugin's context with its settings section, metadata and logger.
    /// </summary>
    public PluginContext Context { get; }

    /// <summary>
    /// Gets the plugin's metadata. Shortcut for <c>Context.Metadata</c>.
    /// </summary>
    public PluginMetadata Metadata => Context.Metadata;

    /// <summary>
    /// Gets where the plugin comes from, for messages and log entries.
    /// </summary>
    internal string Source { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{PluginType.FullName} ({Metadata})";
}
