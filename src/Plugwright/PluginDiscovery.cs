using Microsoft.Extensions.Logging;

namespace Plugwright;

/// <summary>
/// A plugin source that finds its plugins itself, such as the package source of Plugwright.NuGet.
/// </summary>
internal interface IDiscoveringPluginSource
{
    /// <summary>
    /// Finds, verifies and reads the plugins of the source, without loading them for execution.
    /// </summary>
    IEnumerable<PluginUnit> Discover(PluginDiscoveryContext context);
}

/// <summary>
/// What a plugin source can use while it discovers plugins.
/// </summary>
internal sealed class PluginDiscoveryContext
{
    private readonly Func<PluginSource, string, string, IReadOnlyList<PluginFile>, bool> _verify;
    private readonly Action<string, string, string, Exception?> _fail;

    public PluginDiscoveryContext(
        string contentRoot,
        bool verificationEnabled,
        ILogger logger,
        Func<PluginSource, string, string, IReadOnlyList<PluginFile>, bool> verify,
        Action<string, string, string, Exception?> fail)
    {
        ContentRoot = contentRoot;
        VerificationEnabled = verificationEnabled;
        Logger = logger;
        _verify = verify;
        _fail = fail;
    }

    /// <summary>
    /// Gets the host's content root, for resolving relative paths.
    /// </summary>
    public string ContentRoot { get; }

    /// <summary>
    /// Gets whether verifiers are registered. Without verifiers, nothing needs to be read for verification.
    /// </summary>
    public bool VerificationEnabled { get; }

    /// <summary>
    /// Gets the logger of the plugin system (buffered in phase 1).
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>
    /// Lets all responsible verifiers check a plugin's files. Logs and records the results, and reports a failure.
    /// </summary>
    /// <returns><see langword="true"/> if all responsible verifiers passed.</returns>
    public bool Verify(PluginSource source, string name, string location, IReadOnlyList<PluginFile> files) =>
        _verify(source, name, location, files);

    /// <summary>
    /// Reports a plugin that failed before it became a unit: throws in fail-fast mode, otherwise logs and records it.
    /// </summary>
    public void Fail(string pluginId, string location, string reason, Exception? exception) =>
        _fail(pluginId, location, reason, exception);

    /// <summary>
    /// Creates a file for verification.
    /// </summary>
    public static PluginFile CreateFile(string path, byte[] content) => new(path, content);
}
