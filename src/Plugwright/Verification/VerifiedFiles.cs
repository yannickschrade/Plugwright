using System.Security.Cryptography;

namespace Plugwright;

/// <summary>
/// The files of a plugin folder as they were verified, by full path. Assemblies are loaded from these bytes,
/// so that nothing can be replaced between verification and loading.
/// </summary>
internal sealed class VerifiedFiles
{
    private static readonly StringComparer s_pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly Dictionary<string, byte[]> _contentByPath;

    private VerifiedFiles(Dictionary<string, byte[]> contentByPath)
    {
        _contentByPath = contentByPath;
    }

    /// <summary>
    /// Reads all files of a plugin folder into memory, for verification.
    /// </summary>
    public static (VerifiedFiles Files, List<PluginFile> PluginFiles) Read(string folder)
    {
        var contentByPath = new Dictionary<string, byte[]>(s_pathComparer);
        List<PluginFile> pluginFiles = [];

        foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var content = File.ReadAllBytes(path);
            contentByPath.Add(Path.GetFullPath(path), content);
            pluginFiles.Add(new PluginFile(Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/'), content));
        }

        return (new VerifiedFiles(contentByPath), pluginFiles);
    }

    /// <summary>
    /// Returns the verified files below a folder, by path relative to it with '/' as separator. The bytes are not copied.
    /// </summary>
    public Dictionary<string, byte[]> GetFilesBelow(string folder)
    {
        var prefix = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return _contentByPath
            .Where(entry => entry.Key.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            .ToDictionary(
                entry => entry.Key[prefix.Length..].Replace(Path.DirectorySeparatorChar, '/'),
                entry => entry.Value,
                s_pathComparer);
    }

    public bool TryGetContent(string path, out byte[] content) =>
        _contentByPath.TryGetValue(Path.GetFullPath(path), out content!);

    /// <summary>
    /// Returns the verified content of a file, or throws if the file was not part of the verification.
    /// </summary>
    public byte[] GetContent(string path) => TryGetContent(path, out var content)
        ? content
        : throw new FileLoadException($"'{path}' was not verified and is not loaded. It was added after the plugin was verified.");

    /// <summary>
    /// Throws if a file on disk differs from its verified content. Used for native libraries, which can only be loaded from disk.
    /// </summary>
    public void ThrowIfChanged(string path)
    {
        var verified = GetContent(path);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(verified), SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new FileLoadException($"'{path}' changed after the plugin was verified and is not loaded.");
        }
    }
}
