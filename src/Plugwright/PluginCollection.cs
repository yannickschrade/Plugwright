using System.Diagnostics.CodeAnalysis;

namespace Plugwright;

/// <summary>
/// The plugins of a host application, available as <c>builder.Plugins</c>.
/// </summary>
/// <remarks>
/// Adding plugins only records types and directories. Nothing is loaded or run until <c>builder.ApplyPlugins()</c>;
/// then the plugin handlers decide what happens with each plugin. Plugins cannot be added after that call.
/// </remarks>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Collection of plugin sources, named like IServiceCollection.")]
public sealed class PluginCollection
{
    private readonly PluginSystem _system;
    private readonly List<PluginSource> _sources = [];

    internal PluginCollection(PluginSystem system)
    {
        _system = system;
    }

    /// <summary>
    /// Gets the options of the plugin system.
    /// </summary>
    public PluginOptions Options { get; } = new();

    internal IReadOnlyList<PluginSource> Sources => _sources;

    /// <summary>
    /// Adds a plugin type from the application itself or from a referenced assembly.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin type. Usually it implements <see cref="IPlugin"/>.</typeparam>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">The type has already been added.</exception>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.Add&lt;HeartbeatPlugin&gt;();
    /// </code>
    /// </example>
    public PluginCollection Add<TPlugin>()
        where TPlugin : class, new()
    {
        return Add(typeof(TPlugin));
    }

    /// <summary>
    /// Adds a plugin type from the application itself or from a referenced assembly.
    /// </summary>
    /// <param name="pluginType">The plugin type. It must be a non-abstract class with a public parameterless constructor.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">The type is not a valid plugin type or has already been added.</exception>
    public PluginCollection Add(Type pluginType)
    {
        ArgumentNullException.ThrowIfNull(pluginType);
        _system.ThrowIfApplied("Plugins cannot be added");

        if (!pluginType.IsClass || pluginType.IsAbstract || pluginType.ContainsGenericParameters)
        {
            throw new ArgumentException(
                $"Plugin type '{pluginType}' must be a non-abstract, non-generic class.", nameof(pluginType));
        }

        if (pluginType.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new ArgumentException(
                $"Plugin type '{pluginType}' must have a public parameterless constructor. "
                + "Plugins are created before the service container exists, so they cannot receive services through their constructor.",
                nameof(pluginType));
        }

        if (_sources.OfType<TypePluginSource>().Any(source => source.PluginType == pluginType))
        {
            throw new ArgumentException($"Plugin type '{pluginType}' has already been added.", nameof(pluginType));
        }

        _sources.Add(new TypePluginSource(pluginType));
        return this;
    }

    /// <summary>
    /// Adds all plugins from a plugin directory. Each plugin lives in its own subfolder and is loaded
    /// into its own <see cref="System.Runtime.Loader.AssemblyLoadContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The directory is read when <c>builder.ApplyPlugins()</c> runs. Every subfolder must contain an assembly with the
    /// folder's name, for example <c>plugins/Acme.Heartbeat/Acme.Heartbeat.dll</c>, together with its dependencies.
    /// Every public type in that assembly that a registered handler can handle is a plugin.
    /// </para>
    /// <para>
    /// A relative path is resolved against the host's content root, <c>builder.Environment.ContentRootPath</c>.
    /// If the directory does not exist, a warning is logged and no plugins are loaded from it.
    /// Assemblies directly in the directory, outside a subfolder, are ignored with a warning.
    /// </para>
    /// <para>
    /// Plugins run with full trust inside the host process.
    /// </para>
    /// </remarks>
    /// <param name="path">The plugin directory, absolute or relative to the content root.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">The path is empty.</exception>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.AddFromDirectory("plugins");
    /// </code>
    /// </example>
    public PluginCollection AddFromDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _system.ThrowIfApplied("Plugins cannot be added");

        _sources.Add(new DirectoryPluginSource(path));
        return this;
    }
}
