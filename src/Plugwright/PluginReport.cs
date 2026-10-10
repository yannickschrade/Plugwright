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

/// <summary>
/// The result of one verifier for one plugin, as part of the <see cref="PluginReport"/>.
/// </summary>
public sealed class PluginVerification
{
    internal PluginVerification(string pluginName, string location, string verifier, bool succeeded, string message)
    {
        PluginName = pluginName;
        Location = location;
        Verifier = verifier;
        Succeeded = succeeded;
        Message = message;
    }

    /// <summary>
    /// Gets the name the plugin is installed under, for a plugin directory the name of its folder.
    /// </summary>
    public string PluginName { get; }

    /// <summary>
    /// Gets where the plugin is installed, for example the full path of its plugin folder.
    /// </summary>
    public string Location { get; }

    /// <summary>
    /// Gets the name of the verifier type, for example <c>HashVerifier</c>.
    /// </summary>
    public string Verifier { get; }

    /// <summary>
    /// Gets whether the plugin passed this verifier.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets what was checked, or why verification failed.
    /// </summary>
    public string Message { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{PluginName}: {Verifier} {(Succeeded ? "passed" : "failed")}: {Message}";
}
