namespace Plugwright;

/// <summary>
/// Checks the integrity of a plugin before any of its files is read as an assembly.
/// </summary>
/// <remarks>
/// <para>
/// Verification is optional. Without registered verifiers, plugins are not verified. Register verifiers with
/// <c>builder.PluginVerifiers</c>, for example <see cref="PluginVerifierCollection.AddHashVerifier(Action{HashVerifierOptions})"/>.
/// </para>
/// <para>
/// For every plugin, all verifiers whose <see cref="CanVerify"/> returns <see langword="true"/> must succeed. If
/// verifiers are registered but none of them can verify a plugin, the plugin fails. Plugins added in code are part of
/// the host application and are never verified.
/// </para>
/// <para>
/// A verifier receives the plugin's files in memory. When all verifiers succeed, Plugwright loads the plugin from exactly
/// these bytes, so the files cannot be replaced between verification and loading.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// public sealed class MaxSizeVerifier : IPluginVerifier
/// {
///     public bool CanVerify(PluginSource source) =&gt; source is PluginDirectorySource;
///
///     public PluginVerificationResult Verify(PluginVerificationRequest request)
///     {
///         var size = request.Files.Sum(file =&gt; file.Content.Length);
///         return size &lt;= 50_000_000
///             ? PluginVerificationResult.Success($"{size} bytes")
///             : PluginVerificationResult.Failure($"The plugin is {size} bytes, more than 50 MB.");
///     }
/// }
///
/// builder.PluginVerifiers.Add(new MaxSizeVerifier());
/// </code>
/// </example>
public interface IPluginVerifier
{
    /// <summary>
    /// Returns whether this verifier can verify plugins from the given source.
    /// </summary>
    /// <param name="source">The source the plugin comes from, for example a <see cref="PluginDirectorySource"/>.</param>
    /// <returns><see langword="true"/> if the verifier is responsible for plugins from this source.</returns>
    bool CanVerify(PluginSource source);

    /// <summary>
    /// Verifies one plugin.
    /// </summary>
    /// <param name="request">The plugin's files and where they come from.</param>
    /// <returns>Whether the plugin passed, with a message for the log.</returns>
    PluginVerificationResult Verify(PluginVerificationRequest request);
}
