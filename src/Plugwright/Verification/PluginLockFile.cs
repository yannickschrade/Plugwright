using System.Text.Json;

namespace Plugwright;

/// <summary>
/// The lock file of the <see cref="HashVerifier"/>: for every plugin folder, the hash of every file.
/// </summary>
internal sealed class PluginLockFile
{
    public const int CurrentVersion = 1;

    private PluginLockFile(Dictionary<string, Dictionary<string, string>> plugins)
    {
        Plugins = plugins;
    }

    /// <summary>
    /// Gets the expected file hashes by plugin folder name (case-insensitive), then by relative file path.
    /// </summary>
    public IReadOnlyDictionary<string, Dictionary<string, string>> Plugins { get; }

    public static PluginLockFile Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"The lock file '{path}' does not exist.");
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;

            if (!root.TryGetProperty("version", out var version) || version.GetInt32() != CurrentVersion)
            {
                throw new InvalidOperationException($"The lock file '{path}' has an unsupported version. Expected version {CurrentVersion}.");
            }

            var plugins = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var plugin in root.GetProperty("plugins").EnumerateObject())
            {
                var files = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var file in plugin.Value.GetProperty("files").EnumerateObject())
                {
                    files.Add(file.Name, file.Value.GetString()!);
                }

                plugins.Add(plugin.Name, files);
            }

            return new PluginLockFile(plugins);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or ArgumentException or FormatException)
        {
            throw new InvalidOperationException($"The lock file '{path}' is invalid: {exception.Message}", exception);
        }
    }
}
