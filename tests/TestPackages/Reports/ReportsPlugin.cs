using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright.TestPackages.Greeter;

namespace Plugwright.TestPackages.Reports;

public sealed class ReportsPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Configuration["TestPackages:Order"] += "Reports;";
        context.Builder.Configuration["TestPackages:Reports:Dependencies"] = string.Join(",", context.Metadata.Dependencies);
        context.Builder.Services.AddHostedService<ReportsService>();
    }
}

/// <summary>
/// Receives the Greeting registered by the Greeter plugin package; resolving it only works if both share the type.
/// </summary>
public sealed class ReportsService(Greeting greeting) : IHostedService
{
    public string Text => greeting.Text;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
