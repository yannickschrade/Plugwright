using Microsoft.Extensions.DependencyInjection;

namespace Plugwright.TestPlugins.Faulty;

/// <summary>
/// Fails where its settings say: <c>Plugwright:Plugins:Plugwright.TestPlugins.Faulty:Settings:ThrowIn</c> is
/// <c>Configure</c> or <c>Startup</c>. Without the setting it works and registers <see cref="FaultyMarker"/>.
/// </summary>
public sealed class FaultyPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        var throwIn = context.Configuration["ThrowIn"];
        context.Builder.Services.AddSingleton<FaultyMarker>();
        if (throwIn == "Configure")
        {
            throw new InvalidOperationException("Faulty plugin failed in Configure on purpose.");
        }

        if (throwIn == "Startup")
        {
            context.AddStartup<FaultyStartup>();
        }
    }
}

public sealed class FaultyMarker;

public sealed class FaultyStartup : IPluginStartup
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Faulty plugin failed in its startup task on purpose.");
}
