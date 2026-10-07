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
    private const string CodeSource = "added in code";

    private readonly LogBuffer _logBuffer = new();
    private readonly List<ActivePlugin> _activePlugins = [];
    private bool _applied;
    private bool _used;

    private PluginSystem()
    {
        Plugins = new PluginCollection(this);
        Handlers = new PluginHandlerCollection(this);
    }

    public PluginCollection Plugins { get; }

    public PluginHandlerCollection Handlers { get; }

    private ILogger Logger => _logBuffer.CreateLogger(LoggerCategory);

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
        if (_applied)
        {
            throw new InvalidOperationException(
                $"{action} after builder.ApplyPlugins() has been called. Add plugins and handlers before calling ApplyPlugins().");
        }
    }

    /// <summary>
    /// Phase 1: finds all plugins, creates every plugin once and lets all matching handlers apply it.
    /// </summary>
    public void Apply(IHostApplicationBuilder builder)
    {
        if (_applied)
        {
            throw new InvalidOperationException(
                "builder.ApplyPlugins() has already been called. Call it exactly once, before builder.Build().");
        }

        _applied = true;

        var handlers = Handlers.CreateHandlers();
        var candidates = new List<PluginCandidate>();
        foreach (var source in Plugins)
        {
            switch (source)
            {
                case PluginTypeSource typeSource:
                    var metadata = PluginMetadataReader.Read(typeSource.PluginType.Assembly);
                    candidates.Add(new PluginCandidate(typeSource.PluginType, metadata, CodeSource));
                    break;

                case PluginDirectorySource directorySource:
                    var directory = Path.GetFullPath(directorySource.Path, builder.Environment.ContentRootPath);
                    candidates.AddRange(DiscoverDirectory(directory, handlers));
                    break;
            }
        }

        ThrowOnDuplicateIds(candidates);

        foreach (var candidate in candidates)
        {
            ApplyPlugin(builder, candidate, handlers);
        }

        builder.Services.AddSingleton(this);
    }

    /// <summary>
    /// Phase 2: writes the buffered log entries and lets all matching handlers use the built host.
    /// </summary>
    public void Use(IHost app)
    {
        if (_used)
        {
            throw new InvalidOperationException("app.UsePlugins() has already been called. Call it exactly once, after builder.Build().");
        }

        _used = true;

        var loggerFactory = app.Services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        _logBuffer.Flush(loggerFactory);

        foreach (var (plugin, handlers) in _activePlugins)
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
                        $"Plugin '{plugin.Metadata.Id}' ({plugin.Source}): Phase 2 failed for plugin type '{plugin.PluginType}' "
                        + $"in handler '{handler.GetType()}': {exception.Message}",
                        plugin.Metadata.Id,
                        exception);
                }
            }
        }

        var logger = loggerFactory.CreateLogger(LoggerCategory);
        foreach (var (plugin, _) in _activePlugins)
        {
            PluginLog.PluginLoaded(logger, plugin.Metadata.Id, plugin.Metadata.Version, plugin.PluginType.FullName, plugin.Source);
        }
    }

    private List<PluginCandidate> DiscoverDirectory(string directory, List<IPluginHandler> handlers)
    {
        if (!Directory.Exists(directory))
        {
            PluginLog.PluginDirectoryNotFound(Logger, directory);
            return [];
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.dll").Order(StringComparer.Ordinal))
        {
            PluginLog.AssemblyOutsidePluginFolder(Logger, file, directory);
        }

        var candidates = new List<PluginCandidate>();
        foreach (var folder in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
        {
            candidates.AddRange(DiscoverFolder(folder, handlers));
        }

        return candidates;
    }

    /// <summary>
    /// Loads the main assembly of one plugin folder into its own load context and finds its plugin types.
    /// </summary>
    private IEnumerable<PluginCandidate> DiscoverFolder(string folder, List<IPluginHandler> handlers)
    {
        var name = Path.GetFileName(folder);
        var mainAssemblyFile = $"{name}.dll";
        var mainAssemblyPath = Path.Combine(folder, mainAssemblyFile);
        if (!File.Exists(mainAssemblyPath))
        {
            Fail(
                name,
                folder,
                $"The folder contains no '{mainAssemblyFile}'. Each plugin folder must contain an assembly with the folder's name.",
                exception: null);
            return [];
        }

        Assembly assembly;
        Type[] exportedTypes;
        try
        {
            assembly = new PluginLoadContext(mainAssemblyPath).LoadFromAssemblyPath(mainAssemblyPath);
            exportedTypes = assembly.GetExportedTypes();
        }
        catch (Exception exception)
        {
            Fail(name, folder, $"'{mainAssemblyFile}' could not be loaded: {exception.Message}", exception);
            return [];
        }

        var metadata = PluginMetadataReader.Read(assembly);
        var pluginTypes = exportedTypes
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters)
            .Where(type => handlers.Any(handler => handler.CanHandle(type)))
            .ToList();

        if (pluginTypes.Count == 0)
        {
            Fail(
                metadata.Id,
                folder,
                $"'{mainAssemblyFile}' contains no public plugin type that a registered handler can handle.",
                exception: null);
            return [];
        }

        return pluginTypes.Select(type => new PluginCandidate(type, metadata, folder));
    }

    /// <summary>
    /// Throws if two different assemblies provide the same plugin ID. Plugin types from one assembly share its ID.
    /// </summary>
    private static void ThrowOnDuplicateIds(List<PluginCandidate> candidates)
    {
        var firstById = new Dictionary<string, PluginCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (!firstById.TryAdd(candidate.Metadata.Id, candidate)
                && firstById[candidate.Metadata.Id] is var first
                && first.PluginType.Assembly != candidate.PluginType.Assembly)
            {
                throw new PluginException(
                    $"Plugin ID '{candidate.Metadata.Id}' is provided twice: {Describe(first)} and {Describe(candidate)}. "
                    + "Remove one of them.",
                    candidate.Metadata.Id,
                    innerException: null);
            }
        }

        static string Describe(PluginCandidate candidate) => candidate.Source == CodeSource
            ? $"by type '{candidate.PluginType}' added in code"
            : $"by folder '{candidate.Source}'";
    }

    private void ApplyPlugin(IHostApplicationBuilder builder, PluginCandidate candidate, List<IPluginHandler> allHandlers)
    {
        var (pluginType, metadata, source) = candidate;

        var handlers = allHandlers.Where(handler => handler.CanHandle(pluginType)).ToList();
        if (handlers.Count == 0)
        {
            Fail(
                metadata.Id,
                source,
                $"No plugin handler can handle plugin type '{pluginType}'. Implement {nameof(IPlugin)}, "
                + "or register a handler for it with builder.PluginHandlers.Add<T>().",
                exception: null);
            return;
        }

        if (pluginType.GetConstructor(Type.EmptyTypes) is null)
        {
            Fail(
                metadata.Id,
                source,
                $"Plugin type '{pluginType}' has no public parameterless constructor. "
                + "Plugins are created before the service container exists, so they cannot receive services through their constructor.",
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
            Fail(metadata.Id, source, $"Plugin type '{pluginType}' could not be created: {cause.Message}", cause);
            return;
        }

        var context = new PluginContext(
            builder,
            builder.Configuration.GetSection(PluginConfiguration.SettingsSectionPath(metadata.Id)),
            metadata,
            _logBuffer.CreateLogger(pluginType.FullName ?? pluginType.Name));
        var plugin = new PluginDescriptor(pluginType, instance, context, source);

        foreach (var handler in handlers)
        {
            try
            {
                handler.Apply(plugin, builder);
            }
            catch (Exception exception)
            {
                Fail(
                    metadata.Id,
                    source,
                    $"Phase 1 failed for plugin type '{pluginType}' in handler '{handler.GetType()}': {exception.Message}",
                    exception);
                return;
            }
        }

        _activePlugins.Add(new ActivePlugin(plugin, handlers));
    }

    private void Fail(string pluginId, string source, string reason, Exception? exception)
    {
        if (Plugins.Options.FailureMode == PluginFailureMode.FailFast)
        {
            throw new PluginException($"Plugin '{pluginId}' ({source}): {reason}", pluginId, exception);
        }

        PluginLog.PluginSkipped(Logger, exception, pluginId, source, reason);
    }

    /// <summary>
    /// A plugin type found in a source, before it is created.
    /// </summary>
    /// <param name="PluginType">The plugin type.</param>
    /// <param name="Metadata">The metadata of the plugin's assembly.</param>
    /// <param name="Source">Where the plugin comes from: <see cref="CodeSource"/> or the plugin folder.</param>
    private sealed record PluginCandidate(Type PluginType, PluginMetadata Metadata, string Source);

    private sealed record ActivePlugin(PluginDescriptor Plugin, List<IPluginHandler> Handlers);
}
