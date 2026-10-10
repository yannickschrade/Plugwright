namespace Plugwright;

/// <summary>
/// A startup task as recorded in the service collection, for the plugin system to run.
/// </summary>
internal sealed record PluginStartupRegistration(string PluginId, Type StartupType);
