namespace Plugwright;

/// <summary>
/// A prefix of assembly names that matches only at a dot: <c>Acme</c> matches <c>Acme</c> and <c>Acme.Orders</c>,
/// but not <c>AcmeX</c>. Used to share and to scan assemblies.
/// </summary>
internal static class AssemblyNamePrefix
{
    public static bool Matches(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        && (name.Length == prefix.Length || name[prefix.Length] == '.');

    /// <exception cref="ArgumentException">The prefix is empty, or starts or ends with a dot.</exception>
    public static void ThrowIfInvalid(string prefix, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix, paramName);
        if (prefix.StartsWith('.') || prefix.EndsWith('.'))
        {
            throw new ArgumentException(
                $"The prefix '{prefix}' must not start or end with a dot. It is compared at dots: "
                + "'Acme' matches 'Acme' and 'Acme.Orders'.",
                paramName);
        }
    }
}
