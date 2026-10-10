using System.Text.Encodings.Web;
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

    /// <summary>
    /// Creates a lock file for a plugin directory or package folder: the hash of every file in every subfolder,
    /// and of every .nupkg file. Other files directly in the directory are not part of any plugin and are left out.
    /// </summary>
    public static PluginLockFile Create(string pluginDirectory) => Create([pluginDirectory]);

    /// <summary>
    /// Creates one lock file for several plugin directories or package folders, for example a package folder and the
    /// folder that downloaded packages are stored in.
    /// </summary>
    /// <exception cref="InvalidOperationException">Two directories contain a plugin or package with the same name.</exception>
    public static PluginLockFile Create(IEnumerable<string> pluginDirectories)
    {
        var plugins = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pluginDirectory in pluginDirectories)
        {
            foreach ((var name, var files) in CreateEntries(pluginDirectory))
            {
                if (!plugins.TryAdd(name, files))
                {
                    throw new InvalidOperationException($"'{name}' is in more than one of the directories.");
                }
            }
        }

        return new PluginLockFile(plugins);
    }

    private static Dictionary<string, Dictionary<string, string>> CreateEntries(string pluginDirectory)
    {
        var plugins = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in Directory.EnumerateFiles(pluginDirectory, "*.nupkg").Order(StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(package);
            plugins.Add(
                Path.GetFileNameWithoutExtension(package),
                new Dictionary<string, string>(StringComparer.Ordinal) { [fileName] = PluginFileHash.Compute(File.ReadAllBytes(package)) });
        }

        foreach (var folder in Directory.EnumerateDirectories(pluginDirectory).Order(StringComparer.Ordinal))
        {
            (var _, var files) = VerifiedFiles.Read(folder);
            plugins.Add(
                Path.GetFileName(folder),
                files.ToDictionary(file => file.Path, file => PluginFileHash.Compute(file.Content.Span), StringComparer.Ordinal));
        }

        return plugins;
    }

    /// <summary>
    /// Writes the lock file as indented JSON, with plugins and files in ordinal order, so that it diffs well.
    /// </summary>
    public void Write(string path)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        writer.WriteStartObject();
        writer.WriteNumber("version", CurrentVersion);
        writer.WriteStartObject("plugins");
        foreach ((var plugin, var files) in Plugins.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(plugin);
            writer.WriteStartObject("files");
            foreach ((var file, var hash) in files.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteString(file, hash);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

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
