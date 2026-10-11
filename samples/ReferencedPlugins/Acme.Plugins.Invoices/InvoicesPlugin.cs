using Microsoft.Extensions.Logging;
using Plugwright;

namespace Acme.Plugins.Invoices;

public sealed class InvoicesPlugin : IPlugin
{
    public void Configure(PluginContext context) => context.Logger.LogInformation("Invoices configured");
}
