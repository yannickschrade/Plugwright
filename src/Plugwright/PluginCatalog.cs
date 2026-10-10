namespace Plugwright;

/// <summary>
/// The catalog of all known plugins. Entries are immutable; an update replaces an entry under a lock, so readers
/// always see a consistent snapshot.
/// </summary>
internal sealed class PluginCatalog : IPluginCatalog
{
    private readonly Lock _lock = new();
    private PluginInfo[] _plugins = [];

    public IReadOnlyList<PluginInfo> Plugins => Volatile.Read(ref _plugins);

    public PluginInfo? Find(string pluginId)
    {
        ArgumentNullException.ThrowIfNull(pluginId);
        return Array.Find(Volatile.Read(ref _plugins), plugin => string.Equals(plugin.Id, pluginId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds a plugin, or replaces the entry with the same ID.
    /// </summary>
    public void Set(PluginInfo plugin)
    {
        lock (_lock)
        {
            var index = Array.FindIndex(_plugins, existing => string.Equals(existing.Id, plugin.Id, StringComparison.OrdinalIgnoreCase));
            var plugins = index < 0 ? [.. _plugins, plugin] : _plugins.ToArray();
            if (index >= 0)
            {
                plugins[index] = plugin;
            }

            Volatile.Write(ref _plugins, plugins);
        }
    }
}
