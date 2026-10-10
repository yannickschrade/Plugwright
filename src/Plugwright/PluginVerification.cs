namespace Plugwright;

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
