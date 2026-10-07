namespace Plugwright;

/// <summary>
/// Where plugins come from, as recorded by <see cref="PluginCollection"/>.
/// </summary>
internal abstract record PluginSource;

/// <summary>
/// A plugin type added in code with <see cref="PluginCollection.Add(Type)"/>.
/// </summary>
internal sealed record TypePluginSource(Type PluginType) : PluginSource;

/// <summary>
/// A plugin directory added with <see cref="PluginCollection.AddFromDirectory(string)"/>.
/// </summary>
internal sealed record DirectoryPluginSource(string Path) : PluginSource;
