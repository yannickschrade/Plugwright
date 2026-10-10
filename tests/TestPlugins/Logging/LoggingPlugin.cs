using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plugwright;

[assembly: Plugin(Description = "Ignored, the project sets a description.", Authors = "Logging team")]

namespace Plugwright.TestPlugins.Logging;

/// <summary>
/// A type that dependent plugins use. It must be the same type for all of them.
/// </summary>
public sealed class LogSink
{
    public List<string> Entries { get; } = [];
}

public sealed class LoggingPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        if (context.Configuration["Fail"] == "true")
        {
            throw new InvalidOperationException("Logging failed on purpose.");
        }

        context.Builder.Configuration["TestPlugins:Order"] += "Logging;";
        context.Builder.Configuration["TestPlugins:Logging:Version"] = context.Metadata.Version;
        context.Builder.Configuration["TestPlugins:Logging:Authors"] = context.Metadata.Authors;
        context.Builder.Configuration["TestPlugins:Logging:Description"] = context.Metadata.Description;
        context.Builder.Services.AddSingleton<LogSink>();
        context.AddStartup<LoggingStartup>();
    }
}

/// <summary>
/// Records that it ran; fails if the plugin's setting FailStartup is true.
/// </summary>
public sealed class LoggingStartup(IConfiguration configuration) : IPluginStartup
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        configuration["TestPlugins:Startup"] += "Logging;";
        return configuration["Plugwright:Plugins:Plugwright.TestPlugins.Logging:Settings:FailStartup"] == "true"
            ? throw new InvalidOperationException("Logging startup failed on purpose.")
            : Task.CompletedTask;
    }
}
