using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Plugwright;

/// <summary>
/// The state of the plugin system for one host builder. Stored in <see cref="IHostApplicationBuilder.Properties"/>
/// during phase 1 and registered as a singleton for phase 2.
/// </summary>
internal sealed class PluginSystem
{
    private const string LoggerCategory = "Plugwright";

    private readonly LogBuffer logBuffer = new();
    private readonly List<ActivePlugin> activePlugins = [];
    private bool applied;
    private bool used;

    private PluginSystem()
    {
        Plugins = new PluginCollection(this);
        Handlers = new PluginHandlerCollection(this);
    }

    public PluginCollection Plugins { get; }

    public PluginHandlerCollection Handlers { get; }

    public static PluginSystem GetOrCreate(IHostApplicationBuilder builder)
    {
        if (builder.Properties.TryGetValue(typeof(PluginSystem), out var value))
        {
            return (PluginSystem)value;
        }

        var system = new PluginSystem();
        builder.Properties[typeof(PluginSystem)] = system;
        return system;
    }

    public void ThrowIfApplied(string action)
    {
        if (applied)
        {
            throw new InvalidOperationException(
                $"{action} after builder.ApplyPlugins() has been called. Add plugins and handlers before calling ApplyPlugins().");
        }
    }

    /// <summary>
    /// Phase 1: creates every plugin once and lets all matching handlers apply it.
    /// </summary>
    public void Apply(IHostApplicationBuilder builder)
    {
        if (applied)
        {
            throw new InvalidOperationException(
                "builder.ApplyPlugins() has already been called. Call it exactly once, before builder.Build().");
        }

        applied = true;

        var handlers = Handlers.CreateHandlers();
        foreach (var pluginType in Plugins)
        {
            ApplyPlugin(builder, pluginType, handlers);
        }

        builder.Services.AddSingleton(this);
    }

    /// <summary>
    /// Phase 2: writes the buffered log entries and lets all matching handlers use the built host.
    /// </summary>
    public void Use(IHost app)
    {
        if (used)
        {
            throw new InvalidOperationException("app.UsePlugins() has already been called. Call it exactly once, after builder.Build().");
        }

        used = true;

        var loggerFactory = app.Services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        logBuffer.Flush(loggerFactory);

        foreach (var (plugin, handlers) in activePlugins)
        {
            foreach (var handler in handlers)
            {
                try
                {
                    handler.Use(plugin, app);
                }
                catch (Exception exception)
                {
                    throw new PluginException(
                        $"Plugin '{plugin.Metadata.Id}' ({plugin.PluginType}): Phase 2 failed in handler '{handler.GetType()}': {exception.Message}",
                        plugin.Metadata.Id,
                        exception);
                }
            }
        }

        var logger = loggerFactory.CreateLogger(LoggerCategory);
        foreach (var (plugin, _) in activePlugins)
        {
            PluginLog.PluginLoaded(logger, plugin.Metadata.Id, plugin.Metadata.Version, plugin.PluginType.FullName);
        }
    }

    private void ApplyPlugin(IHostApplicationBuilder builder, Type pluginType, List<IPluginHandler> allHandlers)
    {
        var metadata = PluginMetadataReader.Read(pluginType.Assembly);

        var handlers = allHandlers.Where(handler => handler.CanHandle(pluginType)).ToList();
        if (handlers.Count == 0)
        {
            Fail(
                metadata,
                pluginType,
                $"No plugin handler can handle this type. Implement {nameof(IPlugin)}, "
                + "or register a handler for it with builder.PluginHandlers.Add<T>().",
                exception: null);
            return;
        }

        object instance;
        try
        {
            instance = Activator.CreateInstance(pluginType)!;
        }
        catch (TargetInvocationException exception)
        {
            var cause = exception.InnerException ?? exception;
            Fail(metadata, pluginType, $"The plugin could not be created: {cause.Message}", cause);
            return;
        }

        var context = new PluginContext(
            builder,
            builder.Configuration.GetSection(PluginConfiguration.SettingsSectionPath(metadata.Id)),
            metadata,
            logBuffer.CreateLogger(pluginType.FullName ?? pluginType.Name));
        var plugin = new PluginDescriptor(pluginType, instance, context);

        foreach (var handler in handlers)
        {
            try
            {
                handler.Apply(plugin, builder);
            }
            catch (Exception exception)
            {
                Fail(
                    metadata,
                    pluginType,
                    $"Phase 1 failed in handler '{handler.GetType()}': {exception.Message}",
                    exception);
                return;
            }
        }

        activePlugins.Add(new ActivePlugin(plugin, handlers));
    }

    private void Fail(PluginMetadata metadata, Type pluginType, string reason, Exception? exception)
    {
        if (Plugins.Options.FailureMode == PluginFailureMode.FailFast)
        {
            throw new PluginException($"Plugin '{metadata.Id}' ({pluginType}): {reason}", metadata.Id, exception);
        }

        PluginLog.PluginSkipped(logBuffer.CreateLogger(LoggerCategory), exception, metadata.Id, pluginType.FullName, reason);
    }

    private sealed record ActivePlugin(PluginDescriptor Plugin, List<IPluginHandler> Handlers);
}
