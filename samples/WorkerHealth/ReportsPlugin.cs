using Plugwright;

namespace WorkerHealth;

/// <summary>
/// Fails without a connection string, so that the sample has a plugin that does not run.
/// </summary>
public sealed class ReportsPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        _ = context.Configuration["ConnectionString"]
            ?? throw new InvalidOperationException("The reports database connection string is missing.");
    }
}
