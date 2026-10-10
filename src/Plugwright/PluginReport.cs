namespace Plugwright;

/// <summary>
/// Details for the host application about what the plugin system did at startup: the active plugin types with their
/// instances, and how plugins were verified. Available from the service container after <c>builder.ApplyPlugins()</c>.
/// </summary>
/// <remarks>
/// The state of every plugin, also of those that were not loaded, is in the <see cref="IPluginCatalog"/>.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var report = app.Services.GetRequiredService&lt;PluginReport&gt;();
///
/// foreach (var plugin in report.Plugins)
/// {
///     Console.WriteLine($"{plugin.Metadata} ({plugin.PluginType})");
/// }
/// </code>
/// </example>
public sealed class PluginReport
{
    internal PluginReport(IReadOnlyList<PluginDescriptor> plugins, IReadOnlyList<PluginVerification> verifications)
    {
        Plugins = plugins;
        Verifications = verifications;
    }

    /// <summary>
    /// Gets the active plugins, one entry per plugin type, in the order they were processed.
    /// </summary>
    public IReadOnlyList<PluginDescriptor> Plugins { get; }

    /// <summary>
    /// Gets the result of every verification, one entry per plugin and verifier. Empty without verifiers.
    /// </summary>
    public IReadOnlyList<PluginVerification> Verifications { get; }
}
