namespace Plugwright;

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
