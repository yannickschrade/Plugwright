namespace Plugwright;

/// <summary>
/// A plugin directory added with <see cref="PluginCollection.AddFromDirectory(string)"/>.
/// </summary>
public sealed class PluginDirectorySource : PluginSource
{
    internal PluginDirectorySource(string path)
    {
        Path = path;
    }

    /// <summary>
    /// Gets the path as it was passed to <see cref="PluginCollection.AddFromDirectory(string)"/>.
    /// A relative path is resolved against the content root when <c>builder.ApplyPlugins()</c> runs.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Returns a description of the source, for example <c>Directory plugins</c>.
    /// </summary>
    /// <returns>A description of the source.</returns>
    public override string ToString() => $"Directory {Path}";
}
