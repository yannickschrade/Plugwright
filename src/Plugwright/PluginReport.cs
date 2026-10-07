namespace Plugwright;

/// <summary>
/// What the plugin system did at startup: which plugins are active, which were skipped, and how they were verified.
/// Available from the service container after <c>builder.ApplyPlugins()</c>.
/// </summary>
/// <example>
/// <code language="csharp">
/// var report = app.Services.GetRequiredService&lt;PluginReport&gt;();
///
/// foreach (var plugin in report.Plugins)
/// {
///     Console.WriteLine($"{plugin.Metadata} ({plugin.PluginType})");
/// }
///
/// foreach (var skipped in report.SkippedPlugins)
/// {
///     Console.WriteLine($"{skipped.PluginId} skipped: {skipped.Reason}");
/// }
/// </code>
/// </example>
public sealed class PluginReport
{
    internal PluginReport(
        IReadOnlyList<PluginDescriptor> plugins,
        IReadOnlyList<SkippedPlugin> skippedPlugins,
        IReadOnlyList<PluginVerification> verifications)
    {
        Plugins = plugins;
        SkippedPlugins = skippedPlugins;
        Verifications = verifications;
    }

    /// <summary>
    /// Gets the active plugins, one entry per plugin type, in the order they were processed.
    /// </summary>
    public IReadOnlyList<PluginDescriptor> Plugins { get; }

    /// <summary>
    /// Gets the plugins that failed and were skipped because <see cref="PluginOptions.FailureMode"/> is
    /// <see cref="PluginFailureMode.SkipAndLog"/>. Empty in fail-fast mode, because a failure stops the application.
    /// </summary>
    public IReadOnlyList<SkippedPlugin> SkippedPlugins { get; }

    /// <summary>
    /// Gets the result of every verification, one entry per plugin and verifier. Empty without verifiers.
    /// </summary>
    public IReadOnlyList<PluginVerification> Verifications { get; }
}

/// <summary>
/// A plugin that failed and was skipped, as part of the <see cref="PluginReport"/>.
/// </summary>
public sealed class SkippedPlugin
{
    internal SkippedPlugin(string pluginId, string source, string reason)
    {
        PluginId = pluginId;
        Source = source;
        Reason = reason;
    }

    /// <summary>
    /// Gets the plugin ID, or the folder name if the plugin failed before its metadata was read.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets where the plugin comes from: <c>added in code</c> or the full path of the plugin folder.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets why the plugin was skipped.
    /// </summary>
    public string Reason { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{PluginId} ({Source}): {Reason}";
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
