using System.Security.Cryptography;

namespace Plugwright;

/// <summary>
/// File hashes in the form <c>sha256-{base64}</c>. The prefix names the algorithm, so that it can change later.
/// </summary>
internal static class PluginFileHash
{
    public const string Sha256Prefix = "sha256-";

    public static string Compute(ReadOnlySpan<byte> content) => Sha256Prefix + Convert.ToBase64String(SHA256.HashData(content));

    /// <summary>
    /// Compares a file with its expected hash.
    /// </summary>
    /// <param name="path">The file path, for the message.</param>
    /// <param name="expectedHash">The expected hash, for example <c>sha256-…</c>.</param>
    /// <param name="content">The file content.</param>
    /// <param name="listName">Where the expected hash comes from, for the message, for example <c>the lock file</c>.</param>
    /// <returns><see langword="null"/> if the file matches, otherwise why not.</returns>
    public static string? Check(string path, string expectedHash, ReadOnlySpan<byte> content, string listName)
    {
        if (!expectedHash.StartsWith(Sha256Prefix, StringComparison.Ordinal))
        {
            return $"File '{path}' has a hash with an unsupported algorithm in {listName}: '{expectedHash.Split('-', 2)[0]}'. Supported is 'sha256'.";
        }

        var matches = CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(expectedHash),
            System.Text.Encoding.ASCII.GetBytes(Compute(content)));
        return matches ? null : $"File '{path}' does not match its hash in {listName}.";
    }
}
