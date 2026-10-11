using Microsoft.Extensions.DependencyInjection;

namespace Plugwright.Scanned.Plugins;

public sealed record ScannedMarker(string Source);

public sealed class AlphaPlugin : IPlugin
{
    public void Configure(PluginContext context) =>
        context.Builder.Services.AddSingleton(new ScannedMarker(nameof(AlphaPlugin)));
}

public sealed class BetaPlugin : IPlugin
{
    public void Configure(PluginContext context) =>
        context.Builder.Services.AddSingleton(new ScannedMarker(nameof(BetaPlugin)));
}

/// <summary>
/// Not a plugin: abstract types are ignored.
/// </summary>
public abstract class ScannedPluginBase : IPlugin
{
    public abstract void Configure(PluginContext context);
}

/// <summary>
/// Not a plugin: no handler can handle it.
/// </summary>
public sealed class Helper
{
}
