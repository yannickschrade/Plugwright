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

    private readonly LogBuffer _logBuffer = new();
    private readonly List<ActivePlugin> _activePlugins = [];
    private readonly List<SkippedPlugin> _skippedPlugins = [];
    private readonly List<PluginVerification> _verifications = [];
    private bool _applied;
    private bool _used;

    private PluginSystem(string contentRoot)
    {
        Plugins = new PluginCollection(this);
        Handlers = new PluginHandlerCollection(this);
        Verifiers = new PluginVerifierCollection(this, contentRoot);
    }

    public PluginCollection Plugins { get; }

    public PluginHandlerCollection Handlers { get; }

    public PluginVerifierCollection Verifiers { get; }

    private ILogger Logger => _logBuffer.CreateLogger(LoggerCategory);

    public static PluginSystem GetOrCreate(IHostApplicationBuilder builder)
    {
        if (builder.Properties.TryGetValue(typeof(PluginSystem), out var value))
        {
            return (PluginSystem)value;
        }

        var system = new PluginSystem(builder.Environment.ContentRootPath);
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
    /// Phase 1: reads the metadata of all plugins, resolves their dependencies, then loads and applies them
    /// in dependency order.
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
        var units = DiscoverUnits(builder);

        ThrowOnDuplicateIds(units);

        foreach (var unit in PluginDependencyResolver.Resolve(units, (unit, reason) => Fail(unit, reason, exception: null)))
        {
            ApplyUnit(builder, unit, handlers);
        }

        builder.Services.AddSingleton(this);
        builder.Services.AddSingleton(new PluginReport(
            [.. _activePlugins.Select(active => active.Plugin)], [.. _skippedPlugins], [.. _verifications]));
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

    /// <summary>
    /// Creates one unit per plugin assembly, in the order the sources were added. Plugin folders are verified
    /// and read, but not loaded for execution.
    /// </summary>
    private List<PluginUnit> DiscoverUnits(IHostApplicationBuilder builder)
    {
        if (Verifiers.Count == 0 && Plugins.OfType<PluginDirectorySource>().Any())
        {
            PluginLog.PluginVerificationDisabled(Logger);
        }

        List<PluginUnit> units = [];
        foreach (var source in Plugins)
        {
            switch (source)
            {
                case PluginTypeSource typeSource:
                    var assembly = typeSource.PluginType.Assembly;
                    var unit = units.FirstOrDefault(unit => unit.Assembly == assembly);
                    if (unit is null)
                    {
                        unit = PluginUnit.FromCode(assembly);
                        units.Add(unit);
                    }

                    unit.PluginTypes.Add(typeSource.PluginType);
                    break;

                case PluginDirectorySource directorySource:
                    var directory = Path.GetFullPath(directorySource.Path, builder.Environment.ContentRootPath);
                    units.AddRange(DiscoverDirectory(directory, directorySource));
                    break;
            }
        }

        return units;
    }

    private List<PluginUnit> DiscoverDirectory(string directory, PluginDirectorySource source)
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

        List<PluginUnit> units = [];
        foreach (var folder in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
        {
            if (DiscoverFolder(folder, source) is { } unit)
            {
                units.Add(unit);
            }
        }

        return units;
    }

    private PluginUnit? DiscoverFolder(string folder, PluginDirectorySource source)
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
            return null;
        }

        VerifiedFiles? verifiedFiles = null;
        if (Verifiers.Count > 0)
        {
            verifiedFiles = VerifyFolder(folder, source);
            if (verifiedFiles is null)
            {
                return null;
            }
        }

        try
        {
            var info = PluginMetadataReader.ReadWithoutLoading(mainAssemblyPath, verifiedFiles?.GetContent(mainAssemblyPath));
            return PluginUnit.FromFolder(info, folder, mainAssemblyPath, verifiedFiles);
        }
        catch (Exception exception)
        {
            Fail(name, folder, $"'{mainAssemblyFile}' could not be read: {exception.Message}", exception);
            return null;
        }
    }

    /// <summary>
    /// Reads all files of a plugin folder into memory and lets every responsible verifier check them.
    /// </summary>
    /// <returns>The verified files, or <see langword="null"/> if verification failed.</returns>
    private VerifiedFiles? VerifyFolder(string folder, PluginDirectorySource source)
    {
        var name = Path.GetFileName(folder);
        var verifiers = Verifiers.Where(verifier => verifier.CanVerify(source)).ToList();
        if (verifiers.Count == 0)
        {
            Fail(
                name,
                folder,
                "No registered plugin verifier can verify plugins from a directory. "
                + "Register one with builder.PluginVerifiers, or remove all verifiers to load plugins without verification.",
                exception: null);
            return null;
        }

        VerifiedFiles verifiedFiles;
        PluginVerificationRequest request;
        try
        {
            (verifiedFiles, var files) = VerifiedFiles.Read(folder);
            request = new PluginVerificationRequest(source, name, folder, files);
        }
        catch (IOException exception)
        {
            Fail(name, folder, $"The plugin files could not be read: {exception.Message}", exception);
            return null;
        }

        foreach (var verifier in verifiers)
        {
            var verifierName = verifier.GetType().Name;
            PluginVerificationResult result;
            try
            {
                result = verifier.Verify(request);
            }
            catch (Exception exception)
            {
                PluginLog.PluginVerificationFailed(Logger, exception, name, folder, verifierName, exception.Message);
                _verifications.Add(new PluginVerification(name, folder, verifierName, succeeded: false, exception.Message));
                Fail(name, folder, $"Verification by {verifierName} failed: {exception.Message}", exception);
                return null;
            }

            if (!result.Succeeded)
            {
                PluginLog.PluginVerificationFailed(Logger, exception: null, name, folder, verifierName, result.Message);
                _verifications.Add(new PluginVerification(name, folder, verifierName, succeeded: false, result.Message));
                Fail(name, folder, $"Verification by {verifierName} failed: {result.Message}", exception: null);
                return null;
            }

            PluginLog.PluginVerified(Logger, name, folder, verifierName, result.Message);
            _verifications.Add(new PluginVerification(name, folder, verifierName, succeeded: true, result.Message));
        }

        return verifiedFiles;
    }

    /// <summary>
    /// Throws if two different assemblies provide the same plugin ID.
    /// </summary>
    private static void ThrowOnDuplicateIds(List<PluginUnit> units)
    {
        var firstById = new Dictionary<string, PluginUnit>(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in units)
        {
            if (!firstById.TryAdd(unit.Id, unit))
            {
                throw new PluginException(
                    $"Plugin ID '{unit.Id}' is provided twice: {firstById[unit.Id].Describe()} and {unit.Describe()}. Remove one of them.",
                    unit.Id,
                    innerException: null);
            }
        }
    }

    private void ApplyUnit(IHostApplicationBuilder builder, PluginUnit unit, List<IPluginHandler> handlers)
    {
        if (unit.Failed)
        {
            return;
        }

        if (unit.DependsOn.FirstOrDefault(dependency => dependency.Failed) is { } failedDependency)
        {
            Fail(unit, $"It depends on plugin '{failedDependency.Id}', which was skipped.", exception: null);
            return;
        }

        if (!unit.IsInCode && !LoadFolder(unit, handlers))
        {
            return;
        }

        foreach (var pluginType in unit.PluginTypes)
        {
            ApplyPlugin(builder, unit, pluginType, handlers);
        }
    }

    /// <summary>
    /// Loads the main assembly of a plugin folder into its own load context and finds its plugin types.
    /// </summary>
    private bool LoadFolder(PluginUnit unit, List<IPluginHandler> handlers)
    {
        var mainAssemblyPath = unit.MainAssemblyPath!;
        var mainAssemblyFile = Path.GetFileName(mainAssemblyPath);
        var dependencies = unit.DependsOn.ToDictionary(dependency => dependency.Id, dependency => dependency.Assembly!);

        Type[] exportedTypes;
        try
        {
            unit.Assembly = new PluginLoadContext(mainAssemblyPath, dependencies, unit.VerifiedFiles).LoadPluginAssembly(mainAssemblyPath);
            exportedTypes = unit.Assembly.GetExportedTypes();
        }
        catch (Exception exception)
        {
            Fail(unit, $"'{mainAssemblyFile}' could not be loaded: {exception.Message}", exception);
            return false;
        }

        unit.PluginTypes.AddRange(exportedTypes
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters)
            .Where(type => handlers.Any(handler => handler.CanHandle(type))));

        if (unit.PluginTypes.Count == 0)
        {
            Fail(unit, $"'{mainAssemblyFile}' contains no public plugin type that a registered handler can handle.", exception: null);
            return false;
        }

        return true;
    }

    private void ApplyPlugin(IHostApplicationBuilder builder, PluginUnit unit, Type pluginType, List<IPluginHandler> allHandlers)
    {
        var handlers = allHandlers.Where(handler => handler.CanHandle(pluginType)).ToList();
        if (handlers.Count == 0)
        {
            Fail(
                unit,
                $"No plugin handler can handle plugin type '{pluginType}'. Implement {nameof(IPlugin)}, "
                + "or register a handler for it with builder.PluginHandlers.Add<T>().",
                exception: null);
            return;
        }

        if (pluginType.GetConstructor(Type.EmptyTypes) is null)
        {
            Fail(
                unit,
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
            Fail(unit, $"Plugin type '{pluginType}' could not be created: {cause.Message}", cause);
            return;
        }

        var context = new PluginContext(
            builder,
            builder.Configuration.GetSection(PluginConfiguration.SettingsSectionPath(unit.Id)),
            unit.Metadata,
            _logBuffer.CreateLogger(pluginType.FullName ?? pluginType.Name));
        var plugin = new PluginDescriptor(pluginType, instance, context, unit.Source);

        foreach (var handler in handlers)
        {
            try
            {
                handler.Apply(plugin, builder);
            }
            catch (Exception exception)
            {
                Fail(
                    unit,
                    $"Phase 1 failed for plugin type '{pluginType}' in handler '{handler.GetType()}': {exception.Message}",
                    exception);
                return;
            }
        }

        _activePlugins.Add(new ActivePlugin(plugin, handlers));
    }

    /// <summary>
    /// Reports a failed unit: throws in fail-fast mode, otherwise logs it and marks it as failed,
    /// so that units depending on it are skipped.
    /// </summary>
    private void Fail(PluginUnit unit, string reason, Exception? exception)
    {
        unit.Failed = true;
        Fail(unit.Id, unit.Source, reason, exception);
    }

    private void Fail(string pluginId, string source, string reason, Exception? exception)
    {
        if (Plugins.Options.FailureMode == PluginFailureMode.FailFast)
        {
            throw new PluginException($"Plugin '{pluginId}' ({source}): {reason}", pluginId, exception);
        }

        PluginLog.PluginSkipped(Logger, exception, pluginId, source, reason);
        _skippedPlugins.Add(new SkippedPlugin(pluginId, source, reason));
    }

    private sealed record ActivePlugin(PluginDescriptor Plugin, List<IPluginHandler> Handlers);
}
