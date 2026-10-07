namespace Plugwright;

/// <summary>
/// Describes a plugin assembly when the project file does not.
/// </summary>
/// <remarks>
/// <para>
/// Plugin metadata comes primarily from the assembly itself: <c>&lt;Description&gt;</c> and <c>&lt;Authors&gt;</c>
/// in the project file. Values in this attribute only fill gaps and never override them.
/// </para>
/// <para>
/// The attribute applies to the whole assembly, because all plugin classes in one assembly share their metadata.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// [assembly: Plugin(Description = "Sends a heartbeat to the monitoring system.", Authors = "Acme")]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class PluginAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the description of the plugin. Used when the assembly has no <c>AssemblyDescription</c>.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the authors of the plugin. Used when the project file sets neither <c>&lt;Authors&gt;</c> nor <c>&lt;Company&gt;</c>.
    /// </summary>
    public string? Authors { get; set; }
}
