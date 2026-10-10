using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Plugwright;

/// <summary>
/// The application-managed plugin settings file as a configuration source. It only reads keys under
/// <c>Plugwright:Plugins</c>, so that a file the application can write cannot change any other setting.
/// </summary>
internal sealed class PluginSettingsConfigurationSource : JsonConfigurationSource
{
    public PluginSettingsConfigurationSource(string fullPath)
    {
        Path = fullPath;
        Optional = true;
        ReloadOnChange = true;
        ResolveFileProvider();
    }

    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new PluginSettingsConfigurationProvider(this);
    }
}

internal sealed class PluginSettingsConfigurationProvider(JsonConfigurationSource source) : JsonConfigurationProvider(source)
{
    private const string AllowedPrefix = PluginConfiguration.PluginsSectionPath + ":";

    public override void Load(Stream stream)
    {
        base.Load(stream);

        foreach (var key in Data.Keys.Where(key => !key.StartsWith(AllowedPrefix, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            Data.Remove(key);
        }
    }
}

/// <summary>
/// Writes the plugin settings file. Saves are serialized and atomic: the file is written next to the target and moved.
/// </summary>
internal sealed class PluginSettingsStore(string filePath) : IPluginSettingsStore, IDisposable
{
    private static readonly JsonSerializerOptions s_writeOptions = new() { WriteIndented = true };
    private static readonly JsonDocumentOptions s_readOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    private readonly SemaphoreSlim _lock = new(1, 1);

    public string FilePath { get; } = filePath;

    public void Dispose() => _lock.Dispose();

    public Task SetEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken = default) =>
        UpdateAsync(pluginId, plugin => plugin["Enabled"] = enabled, cancellationToken);

    public Task SaveSettingsAsync<TSettings>(string pluginId, TSettings settings, CancellationToken cancellationToken = default) =>
        UpdateAsync(pluginId, plugin => plugin["Settings"] = JsonSerializer.SerializeToNode(settings), cancellationToken);

    private async Task UpdateAsync(string pluginId, Action<JsonObject> update, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var root = File.Exists(FilePath)
                ? JsonNode.Parse(await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false), documentOptions: s_readOptions)
                    as JsonObject ?? []
                : [];

            var plugins = GetOrAddObject(GetOrAddObject(root, "Plugwright"), "Plugins");
            update(GetOrAddObject(plugins, pluginId));

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporaryFile = $"{FilePath}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(temporaryFile, root.ToJsonString(s_writeOptions), cancellationToken).ConfigureAwait(false);
            File.Move(temporaryFile, FilePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns the object under a key, compared without case like configuration keys, and adds it if it is missing.
    /// </summary>
    private static JsonObject GetOrAddObject(JsonObject parent, string key)
    {
        var existing = parent.FirstOrDefault(property => string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase));
        if (existing.Value is JsonObject child)
        {
            return child;
        }

        if (existing.Key is not null)
        {
            parent.Remove(existing.Key);
        }

        child = [];
        parent[existing.Key ?? key] = child;
        return child;
    }
}
