namespace Plugwright;

/// <summary>
/// Where plugins come from, as added to <c>builder.Plugins</c>.
/// </summary>
/// <remarks>
/// A source only describes what was added. Plugin directories are read when <c>builder.ApplyPlugins()</c> runs.
/// </remarks>
/// <example>
/// <code language="csharp">
/// foreach (var source in builder.Plugins)
/// {
///     Console.WriteLine(source);   // "Type Heartbeat.HeartbeatPlugin", "Directory plugins"
/// }
/// </code>
/// </example>
public abstract class PluginSource
{
    private protected PluginSource()
    {
    }
}
