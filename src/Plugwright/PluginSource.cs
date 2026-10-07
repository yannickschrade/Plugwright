namespace Plugwright;

/// <summary>
/// Where plugins come from, as added to <c>builder.Plugins</c>.
/// </summary>
/// <remarks>
/// A source only describes what was added. Plugin directories are read when <c>builder.ApplyPlugins()</c> runs.
/// </remarks>
/// <example>
/// <code language="csharp">
/// foreach (var source in builder.Plugins)
/// {
///     Console.WriteLine(source);   // "Type Heartbeat.HeartbeatPlugin", "Directory plugins"
/// }
/// </code>
/// </example>
public abstract class PluginSource
{
    private protected PluginSource()
    {
    }
}

/// <summary>
/// A plugin type added in code with <see cref="PluginCollection.Add{TPlugin}"/> or <see cref="PluginCollection.Add(Type)"/>.
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

/// <summary>
/// A plugin directory added with <see cref="PluginCollection.AddFromDirectory(string)"/>.
/// </summary>
public sealed class PluginDirectorySource : PluginSource
{
    internal PluginDirectorySource(string path)
    {
        Path = path;
    }

    /// <summary>
    /// Gets the path as it was passed to <see cref="PluginCollection.AddFromDirectory(string)"/>.
    /// A relative path is resolved against the content root when <c>builder.ApplyPlugins()</c> runs.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Returns a description of the source, for example <c>Directory plugins</c>.
    /// </summary>
    /// <returns>A description of the source.</returns>
    public override string ToString() => $"Directory {Path}";
}
