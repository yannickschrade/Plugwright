namespace Plugwright;

/// <summary>
/// Verifies plugin folders and packages against a lock file on the host side that lists the hash of every file.
/// </summary>
/// <remarks>
/// <para>
/// A plugin folder passes when the lock file has an entry for the folder, the folder contains exactly the files listed
/// there, and every file matches its hash. A file that is missing, added or changed fails the plugin.
/// A package (with Plugwright.NuGet) passes when the lock file has an entry for its file name without <c>.nupkg</c>,
/// and the package file matches its hash. This applies to plugin packages and library packages alike.
/// The lock file must be regenerated whenever a plugin or package is installed or updated.
/// </para>
/// <para>
/// Register it with <see cref="PluginVerifierCollection.AddHashVerifier(Action{HashVerifierOptions})"/>.
/// </para>
/// </remarks>
/// <example>
/// A lock file with one plugin:
/// <code language="json">
/// {
///   "version": 1,
///   "plugins": {
///     "Acme.Greeter": {
///       "files": {
///         "Acme.Greeter.dll": "sha256-q1MKE+RZFJgrefm34/uplM/R8/si9xzqGvvwK0YMbR0=",
///         "Acme.Greeter.deps.json": "sha256-47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU="
///       }
///     }
///   }
/// }
/// </code>
/// </example>
public sealed class HashVerifier : IPluginVerifier
{
    private readonly string _lockFilePath;
    private readonly Lazy<PluginLockFile> _lockFile;

    internal HashVerifier(string lockFilePath)
    {
        _lockFilePath = lockFilePath;
        _lockFile = new Lazy<PluginLockFile>(() => PluginLockFile.Read(lockFilePath));
    }

    /// <summary>
    /// Returns <see langword="true"/> for every source that loads plugins from files: plugin directories and packages.
    /// </summary>
    /// <param name="source">The source the plugin comes from.</param>
    /// <returns>Whether the source is not a <see cref="PluginTypeSource"/>.</returns>
    public bool CanVerify(PluginSource source) => source is not PluginTypeSource;

    /// <summary>
    /// Compares the plugin's files with the lock file.
    /// </summary>
    /// <param name="request">The plugin to verify.</param>
    /// <returns>Success if all files match the lock file exactly.</returns>
    /// <exception cref="InvalidOperationException">The lock file is missing or invalid.</exception>
    public PluginVerificationResult Verify(PluginVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_lockFile.Value.Plugins.TryGetValue(request.Name, out var expectedHashes))
        {
            return PluginVerificationResult.Failure($"The lock file '{_lockFilePath}' has no entry for plugin '{request.Name}'.");
        }

        foreach (var file in request.Files)
        {
            if (!expectedHashes.TryGetValue(file.Path, out var expectedHash))
            {
                return PluginVerificationResult.Failure($"File '{file.Path}' is not listed in the lock file.");
            }

            if (PluginFileHash.Check(file.Path, expectedHash, file.Content.Span, "the lock file") is { } problem)
            {
                return PluginVerificationResult.Failure(problem);
            }
        }

        var missing = expectedHashes.Keys.Except(request.Files.Select(file => file.Path), StringComparer.Ordinal).Order(StringComparer.Ordinal).FirstOrDefault();
        if (missing is not null)
        {
            return PluginVerificationResult.Failure($"File '{missing}' is listed in the lock file but missing.");
        }

        return PluginVerificationResult.Success(request.Files.Count == 1
            ? $"The hash of '{request.Files[0].Path}' matches the lock file '{_lockFilePath}'."
            : $"The hashes of all {request.Files.Count} files match the lock file '{_lockFilePath}'.");
    }
}
