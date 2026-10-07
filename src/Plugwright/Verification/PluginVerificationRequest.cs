namespace Plugwright;

/// <summary>
/// A plugin to verify: all its files, in memory, and where they come from.
/// </summary>
/// <remarks>
/// Verification runs before the plugin's metadata is read, so the plugin ID is not known yet.
/// <see cref="Name"/> is the name the plugin is installed under, for a plugin directory the name of its folder.
/// </remarks>
public sealed class PluginVerificationRequest
{
    internal PluginVerificationRequest(PluginSource source, string name, string location, IReadOnlyList<PluginFile> files)
    {
        Source = source;
        Name = name;
        Location = location;
        Files = files;
    }

    /// <summary>
    /// Gets the source the plugin comes from.
    /// </summary>
    public PluginSource Source { get; }

    /// <summary>
    /// Gets the name the plugin is installed under, for example the name of its plugin folder.
    /// It is also the expected plugin ID.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets where the plugin is installed, for example the full path of its plugin folder.
    /// </summary>
    public string Location { get; }

    /// <summary>
    /// Gets all files of the plugin, ordered by path.
    /// </summary>
    public IReadOnlyList<PluginFile> Files { get; }
}

/// <summary>
/// One file of a plugin, with its content as read for verification.
/// </summary>
public sealed class PluginFile
{
    internal PluginFile(string path, byte[] content)
    {
        Path = path;
        Content = content;
    }

    /// <summary>
    /// Gets the path relative to the plugin folder, with <c>/</c> as separator, for example <c>runtimes/linux-x64/native/libfoo.so</c>.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the content of the file.
    /// </summary>
    public ReadOnlyMemory<byte> Content { get; }

    /// <inheritdoc/>
    public override string ToString() => Path;
}
