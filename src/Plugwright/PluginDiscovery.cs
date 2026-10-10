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
    private readonly Func<PluginSource, string, string, IReadOnlyList<PluginFile>, string, bool> _verify;
    private readonly Action<string, string, string, Exception?> _fail;
    private readonly Func<string, bool> _isDisabled;

    public PluginDiscoveryContext(
        string contentRoot,
        bool verificationEnabled,
        ILogger logger,
        Func<PluginSource, string, string, IReadOnlyList<PluginFile>, string, bool> verify,
        Action<string, string, string, Exception?> fail,
        Func<string, bool> isDisabled)
    {
        _isDisabled = isDisabled;
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
    /// <summary>
    /// Lets the verifiers check a plugin's files. <paramref name="name"/> is the name the verifiers know the plugin by,
    /// for example in the lock file; <paramref name="pluginId"/> the ID it appears under in the catalog if it fails.
    /// </summary>
    public bool Verify(PluginSource source, string name, string location, IReadOnlyList<PluginFile> files, string pluginId) =>
        _verify(source, name, location, files, pluginId);

    /// <summary>
    /// Reports a plugin that failed before it became a unit: throws in fail-fast mode, otherwise logs and records it.
    /// </summary>
    public void Fail(string pluginId, string location, string reason, Exception? exception) =>
        _fail(pluginId, location, reason, exception);

    /// <summary>
    /// Creates a file for verification.
    /// </summary>
    /// <summary>
    /// Returns whether the configuration turns the plugin with this ID off. Such plugins are neither verified nor loaded.
    /// </summary>
    public bool IsDisabled(string pluginId) => _isDisabled(pluginId);

    public static PluginFile CreateFile(string path, byte[] content) => new(path, content);
}
