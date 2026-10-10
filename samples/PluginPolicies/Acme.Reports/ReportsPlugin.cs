using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plugwright;

namespace Acme.Reports;

/// <summary>
/// Nice to have: the host keeps running without it. Fails when its setting Fail is true.
/// </summary>
public sealed class ReportsPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services.AddSingleton<ReportArchive>();
        if (context.Configuration.GetValue<bool>("Fail"))
        {
            throw new InvalidOperationException("The report archive is not reachable.");
        }
    }
}

public sealed class ReportArchive;
