using Microsoft.Extensions.DependencyInjection;
using Plugwright;

namespace Acme.Billing;

/// <summary>
/// The application cannot work without billing: the host marks this plugin as required.
/// </summary>
public sealed class BillingPlugin : IPlugin
{
    public void Configure(PluginContext context) => context.Builder.Services.AddSingleton<Invoices>();
}

public sealed class Invoices;
