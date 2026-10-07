using System.Collections;

namespace Plugwright;

/// <summary>
/// The plugin handlers of a host application, available as <c>builder.PluginHandlers</c>.
/// </summary>
/// <remarks>
/// The built-in handler for <see cref="IPlugin"/> is always registered and always runs first.
/// Handlers added here run after it, in the order they were added. Handlers cannot be added after
/// <c>builder.ApplyPlugins()</c> has been called.
/// </remarks>
public sealed class PluginHandlerCollection : IReadOnlyCollection<Type>
{
    private readonly PluginSystem _system;
    private readonly List<Type> _handlerTypes = [];

    internal PluginHandlerCollection(PluginSystem system)
    {
        _system = system;
    }

    /// <summary>
    /// Gets the number of handler types added, not counting the built-in handler.
    /// </summary>
    public int Count => _handlerTypes.Count;

    /// <summary>
    /// Adds a plugin handler. It is created with its parameterless constructor when <c>builder.ApplyPlugins()</c> runs.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">The handler type has already been added.</exception>
    /// <example>
    /// <code language="csharp">
    /// builder.PluginHandlers.Add&lt;MenuPluginHandler&gt;();
    /// </code>
    /// </example>
    public PluginHandlerCollection Add<THandler>()
        where THandler : class, IPluginHandler, new()
    {
        _system.ThrowIfApplied("Plugin handlers cannot be added");

        if (_handlerTypes.Contains(typeof(THandler)))
        {
            throw new ArgumentException($"Plugin handler '{typeof(THandler)}' has already been added.", nameof(THandler));
        }

        _handlerTypes.Add(typeof(THandler));
        return this;
    }

    /// <summary>
    /// Returns the added handler types in registration order, not including the built-in handler.
    /// </summary>
    /// <returns>An enumerator over the handler types.</returns>
    public IEnumerator<Type> GetEnumerator() => _handlerTypes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal List<IPluginHandler> CreateHandlers()
    {
        List<IPluginHandler> handlers = [new DefaultPluginHandler()];
        handlers.AddRange(_handlerTypes.Select(type => (IPluginHandler)Activator.CreateInstance(type)!));
        return handlers;
    }
}
