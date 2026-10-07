using System.Reflection;

namespace Plugwright;

/// <summary>
/// Reads plugin metadata from a plugin assembly.
/// </summary>
internal static class PluginMetadataReader
{
    public static PluginMetadata Read(Assembly assembly)
    {
        var name = assembly.GetName();
        return new PluginMetadata(name.Name!, name.Version?.ToString() ?? "0.0.0.0");
    }
}
