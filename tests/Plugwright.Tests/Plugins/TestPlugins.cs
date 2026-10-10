using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Plugwright.Tests.Plugins;

public sealed class Marker
{
    public required string Source { get; init; }
}

public sealed class MarkerPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services.AddSingleton(new Marker { Source = nameof(MarkerPlugin) });
    }
}

public sealed class SecondMarkerPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services.AddSingleton(new Marker { Source = nameof(SecondMarkerPlugin) });
    }
}

/// <summary>
/// Stores its context in the builder properties, so tests can inspect it without static state.
/// </summary>
public sealed class ContextCapturingPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Properties[typeof(ContextCapturingPlugin)] = context;
    }

    public static PluginContext GetContext(IHostApplicationBuilder builder) =>
        (PluginContext)builder.Properties[typeof(ContextCapturingPlugin)];
}

/// <summary>
/// Records calls of plugins and handlers. Shared between both phases through the builder properties and the container.
/// </summary>
public sealed class CallLog
{
    public List<string> Calls { get; } = [];

    public static CallLog From(IHostApplicationBuilder builder)
    {
        if (!builder.Properties.TryGetValue(typeof(CallLog), out var value))
        {
            value = new CallLog();
            builder.Properties[typeof(CallLog)] = value;
            builder.Services.AddSingleton((CallLog)value);
        }

        return (CallLog)value;
    }
}

public sealed class LoggingPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Logger.LogInformation("Configured during phase 1");
    }
}

public sealed class ThrowingPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        throw new InvalidOperationException("Configure went wrong");
    }
}

public sealed class ThrowingConstructorPlugin : IPlugin
{
    public ThrowingConstructorPlugin()
    {
        throw new InvalidOperationException("Constructor went wrong");
    }

    public void Configure(PluginContext context)
    {
    }
}

/// <summary>
/// A plugin type no built-in handler can handle.
/// </summary>
public sealed class UnhandledPlugin
{
}

public interface ITaggedPlugin
{
    string Tag { get; }
}

/// <summary>
/// Handled by both the default handler and <see cref="RecordingHandler"/>.
/// </summary>
public sealed class TaggedPlugin : IPlugin, ITaggedPlugin
{
    public string Tag => "tagged";

    public void Configure(PluginContext context)
    {
        CallLog.From(context.Builder).Calls.Add($"Configure:{RuntimeHelpers.GetHashCode(this)}");
    }
}

public sealed class RecordingHandler : IPluginHandler
{
    public bool CanHandle(Type pluginType) => typeof(ITaggedPlugin).IsAssignableFrom(pluginType);

    public void Apply(PluginDescriptor plugin, IHostApplicationBuilder builder)
    {
        CallLog.From(builder).Calls.Add($"Apply:{RuntimeHelpers.GetHashCode(plugin.Instance)}");
    }

    public void Use(PluginDescriptor plugin, IHost app)
    {
        app.Services.GetRequiredService<CallLog>().Calls.Add($"Use:{RuntimeHelpers.GetHashCode(plugin.Instance)}");
    }
}

public sealed class ApplyOnlyHandler : IPluginHandler
{
    public bool CanHandle(Type pluginType) => typeof(ITaggedPlugin).IsAssignableFrom(pluginType);

    public void Apply(PluginDescriptor plugin, IHostApplicationBuilder builder)
    {
    }
}

public sealed class ThrowingUseHandler : IPluginHandler
{
    public bool CanHandle(Type pluginType) => typeof(ITaggedPlugin).IsAssignableFrom(pluginType);

    public void Apply(PluginDescriptor plugin, IHostApplicationBuilder builder)
    {
    }

    public void Use(PluginDescriptor plugin, IHost app)
    {
        throw new InvalidOperationException("Use went wrong");
    }
}

public abstract class AbstractPlugin : IPlugin
{
    public abstract void Configure(PluginContext context);
}

public sealed class PluginWithoutParameterlessConstructor(string name) : IPlugin
{
    public string Name { get; } = name;

    public void Configure(PluginContext context)
    {
    }
}

public sealed class TypedRecordingHandler : PluginHandler<ITaggedPlugin>
{
    protected override void Apply(ITaggedPlugin instance, PluginDescriptor plugin, IHostApplicationBuilder builder)
    {
        CallLog.From(builder).Calls.Add($"TypedApply:{instance.Tag}:{ReferenceEquals(instance, plugin.Instance)}");
    }

    protected override void Use(ITaggedPlugin instance, PluginDescriptor plugin, IHost app)
    {
        app.Services.GetRequiredService<CallLog>().Calls.Add($"TypedUse:{instance.Tag}:{ReferenceEquals(instance, plugin.Instance)}");
    }
}

public sealed class TypedApplyOnlyHandler : PluginHandler<ITaggedPlugin>
{
    protected override void Apply(ITaggedPlugin instance, PluginDescriptor plugin, IHostApplicationBuilder builder)
    {
        CallLog.From(builder).Calls.Add("TypedApplyOnly");
    }
}

/// <summary>
/// Registers a service, then fails: the service must be taken back.
/// </summary>
public sealed class HalfConfiguredPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services.AddSingleton(new Marker { Source = nameof(HalfConfiguredPlugin) });
        throw new InvalidOperationException("Failed halfway");
    }
}
