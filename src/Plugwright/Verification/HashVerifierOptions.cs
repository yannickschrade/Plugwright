namespace Plugwright;

/// <summary>
/// Settings of the <see cref="HashVerifier"/>.
/// </summary>
public sealed class HashVerifierOptions
{
    /// <summary>
    /// Gets or sets the path of the lock file. A relative path is resolved against the host's content root.
    /// The default is <c>plugins.lock.json</c>.
    /// </summary>
    /// <remarks>
    /// The lock file is the basis of trust. Store it where whoever can write to the plugin directory cannot change it.
    /// </remarks>
    public string LockFile { get; set; } = "plugins.lock.json";
}
