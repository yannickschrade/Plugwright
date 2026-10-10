namespace Plugwright;

/// <summary>
/// What the plugin system knows about one plugin: its metadata and its state. Part of the <see cref="IPluginCatalog"/>.
/// </summary>
/// <remarks>
/// A plugin here is a plugin assembly or package with its own ID. Several plugin classes in one assembly share one
/// entry. If one of them fails, the entry is <see cref="PluginState.Failed"/>.
/// </remarks>
public sealed record PluginInfo
{
    /// <summary>
    /// Gets the plugin ID. For a plugin that failed before its metadata could be read, the name of its folder or
    /// package file.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the plugin version, or <see langword="null"/> if it is not known, for example because the plugin was not
    /// read.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the plugin's description, if it has one.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets where the plugin comes from: <c>added in code</c>, or the full path of its folder or package.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets the plugin's state.
    /// </summary>
    public required PluginState State { get; init; }

    /// <summary>
    /// Gets why the plugin is not loaded, for people to read. <see langword="null"/> for a loaded plugin.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the message of the exception that made the plugin fail, without stack trace, if there was one.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets whether the host application requires this plugin. A required plugin that cannot be loaded stops the
    /// application.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Gets the IDs of the plugins this plugin depends on.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>
    /// Gets the names of the plugin handlers that processed the plugin, for example <c>DefaultPluginHandler</c> and
    /// <c>WebPluginHandler</c>.
    /// </summary>
    public IReadOnlyList<string> AppliedHandlers { get; init; } = [];
}
