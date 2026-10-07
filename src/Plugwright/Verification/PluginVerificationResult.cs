namespace Plugwright;

/// <summary>
/// The result of <see cref="IPluginVerifier.Verify"/>.
/// </summary>
public sealed class PluginVerificationResult
{
    private PluginVerificationResult(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message;
    }

    /// <summary>
    /// Gets whether the plugin passed verification.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets a message for the log: what was checked, or why verification failed.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="message">What was checked, for example <c>sha256 hashes of 6 files match the lock file</c>.</param>
    /// <returns>A successful result.</returns>
    public static PluginVerificationResult Success(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new PluginVerificationResult(succeeded: true, message);
    }

    /// <summary>
    /// Creates a failed result. The plugin is not loaded.
    /// </summary>
    /// <param name="message">Why verification failed, for example <c>File 'Acme.Greeter.dll' has a different hash</c>.</param>
    /// <returns>A failed result.</returns>
    public static PluginVerificationResult Failure(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new PluginVerificationResult(succeeded: false, message);
    }
}
