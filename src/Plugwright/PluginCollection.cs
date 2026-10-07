using System.Collections;

namespace Plugwright;

/// <summary>
/// The plugins of a host application, available as <c>builder.Plugins</c>.
/// </summary>
/// <remarks>
/// Adding a plugin only records its type. What happens with it is decided by the plugin handlers when
/// <c>builder.ApplyPlugins()</c> runs. Plugins cannot be added after that call.
/// </remarks>
public sealed class PluginCollection : IReadOnlyCollection<Type>
{
    private readonly PluginSystem system;
    private readonly List<Type> pluginTypes = [];

    internal PluginCollection(PluginSystem system)
    {
        this.system = system;
    }

    /// <summary>
    /// Gets the options of the plugin system.
    /// </summary>
    public PluginOptions Options { get; } = new();

    /// <summary>
    /// Gets the number of plugin types added.
    /// </summary>
    public int Count => pluginTypes.Count;

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
        system.ThrowIfApplied("Plugins cannot be added");

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

        if (pluginTypes.Contains(pluginType))
        {
            throw new ArgumentException($"Plugin type '{pluginType}' has already been added.", nameof(pluginType));
        }

        pluginTypes.Add(pluginType);
        return this;
    }

    /// <summary>
    /// Returns the added plugin types in the order they were added.
    /// </summary>
    /// <returns>An enumerator over the plugin types.</returns>
    public IEnumerator<Type> GetEnumerator() => pluginTypes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
