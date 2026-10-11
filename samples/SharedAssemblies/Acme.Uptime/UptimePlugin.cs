using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Plugwright;
using SharedAssemblies.Contracts;

namespace Acme.Uptime;

// --8<-- [start:plugin]
public sealed class UptimePlugin : IPlugin
{
    public void Configure(PluginContext context) =>
        context.Builder.Services.AddSingleton<IStatusSource, UptimeStatus>();
}

public sealed class UptimeStatus : IStatusSource
{
    public string Name => "Uptime";

    public string GetStatus() => $"{(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds:F1} s";
}
// --8<-- [end:plugin]
