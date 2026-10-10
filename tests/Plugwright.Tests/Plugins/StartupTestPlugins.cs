using Microsoft.Extensions.DependencyInjection;

namespace Plugwright.Tests.Plugins;

/// <summary>
/// Collects what startup tasks did. The tests register it.
/// </summary>
public sealed class StartupLog
{
    public List<string> Entries { get; } = [];
}

public sealed class ScopedMarker
{
    public Guid Id { get; } = Guid.NewGuid();
}

/// <summary>
/// Adds two startup tasks that record their order and the scoped marker they received.
/// </summary>
public sealed class TwoStartupsPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services.AddScoped<ScopedMarker>();
        context.AddStartup<FirstStartup>().AddStartup<SecondStartup>();
    }
}

public sealed class FirstStartup(StartupLog log, ScopedMarker marker) : IPluginStartup
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        log.Entries.Add($"First:{marker.Id}");
        return Task.CompletedTask;
    }
}

public sealed class SecondStartup(StartupLog log, ScopedMarker marker) : IPluginStartup
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        log.Entries.Add($"Second:{marker.Id}");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Lets a test hold a startup task until it opens the gate.
/// </summary>
public sealed class StartupGate
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Open { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class GatedStartupPlugin : IPlugin
{
    public void Configure(PluginContext context) => context.AddStartup<GatedStartup>();
}

public sealed class GatedStartup(StartupGate gate) : IPluginStartup
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        gate.Entered.SetResult();
        await gate.Open.Task.WaitAsync(cancellationToken);
    }
}

public sealed class FailingStartupPlugin : IPlugin
{
    public void Configure(PluginContext context) => context.AddStartup<FailingStartup>();
}

public sealed class FailingStartup : IPluginStartup
{
    public Task StartAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Startup went wrong");
}
