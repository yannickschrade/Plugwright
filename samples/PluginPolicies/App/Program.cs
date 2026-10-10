using Acme.Billing;
using Acme.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// --8<-- [start:policies]
// The default for all plugins: a failing plugin stops the application.
builder.Plugins.Options.FailureMode = PluginFailureMode.FailFast;

// Billing is required: without it, the application does not start, whatever the failure mode.
builder.Plugins.Add<BillingPlugin>();
builder.Plugins.Require("Acme.Billing");

// Reports are optional: if they fail, the application runs without them.
builder.Plugins.Add<ReportsPlugin>(policy => policy.FailureMode = PluginFailureMode.SkipAndLog);
// --8<-- [end:policies]

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();

foreach (var plugin in app.Services.GetRequiredService<IPluginCatalog>().Plugins)
{
    Console.WriteLine($"{plugin.Id}: {plugin.State}{(plugin.IsRequired ? " (required)" : "")} {plugin.Reason}");
}

Console.WriteLine($"Report archive registered: {app.Services.GetService<ReportArchive>() is not null}");
