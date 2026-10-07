using System.Security.Cryptography;

namespace Plugwright;

/// <summary>
/// Verifies plugin folders against a lock file on the host side that lists the hash of every file of every plugin.
/// </summary>
/// <remarks>
/// <para>
/// A plugin passes when the lock file has an entry for its folder, the folder contains exactly the files listed there,
/// and every file matches its hash. A file that is missing, added or changed fails the plugin.
/// The lock file must be regenerated whenever a plugin is installed or updated.
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
    /// Returns <see langword="true"/> for plugin directories.
    /// </summary>
    /// <param name="source">The source the plugin comes from.</param>
    /// <returns>Whether the source is a <see cref="PluginDirectorySource"/>.</returns>
    public bool CanVerify(PluginSource source) => source is PluginDirectorySource;

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

            if (!PluginFileHash.Matches(expectedHash, file.Content.Span, out var problem))
            {
                return PluginVerificationResult.Failure($"File '{file.Path}' {problem}.");
            }
        }

        var missing = expectedHashes.Keys.Except(request.Files.Select(file => file.Path), StringComparer.Ordinal).Order(StringComparer.Ordinal).FirstOrDefault();
        if (missing is not null)
        {
            return PluginVerificationResult.Failure($"File '{missing}' is listed in the lock file but missing.");
        }

        return PluginVerificationResult.Success($"The hashes of all {request.Files.Count} files match the lock file '{_lockFilePath}'.");
    }
}

/// <summary>
/// File hashes in the form <c>sha256-{base64}</c>. The prefix names the algorithm, so that it can change later.
/// </summary>
internal static class PluginFileHash
{
    public const string Sha256Prefix = "sha256-";

    public static string Compute(ReadOnlySpan<byte> content) => Sha256Prefix + Convert.ToBase64String(SHA256.HashData(content));

    public static bool Matches(string expectedHash, ReadOnlySpan<byte> content, out string problem)
    {
        if (!expectedHash.StartsWith(Sha256Prefix, StringComparison.Ordinal))
        {
            problem = $"has a hash with an unsupported algorithm: '{expectedHash.Split('-', 2)[0]}'. Supported is 'sha256'";
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(expectedHash),
            System.Text.Encoding.ASCII.GetBytes(Compute(content))))
        {
            problem = "does not match its hash in the lock file";
            return false;
        }

        problem = string.Empty;
        return true;
    }
}
