using System.Diagnostics.Metrics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
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
    public const string LoggerCategory = "Plugwright";

    private readonly LogBuffer _logBuffer = new();
    private readonly List<ActivePlugin> _activePlugins = [];
    private readonly List<PluginVerification> _verifications = [];
    private readonly List<(string PluginId, string Problem, Exception? Exception)> _requiredProblems = [];
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

    public PluginConfigurationReaders ConfigurationReaders { get; } = new();

    public PluginCatalog Catalog { get; } = new();

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

        var settingsSource = AddSettingsFile(builder);

        var handlers = Handlers.CreateHandlers();
        foreach (var handler in handlers.OfType<IPluginHandlerSetup>())
        {
            handler.Setup(builder);
        }

        var units = DiscoverUnits(builder);

        ThrowOnDuplicateIds(units);
        FindRequiredPlugins(units);

        var sortedUnits = PluginDependencyResolver.Resolve(units, (unit, state, reason) => Fail(unit, state, reason, exception: null));
        SkipDependentsOfUnavailable(sortedUnits);

        // Problems with required plugins that are known before any plugin runs stop the application right here.
        ThrowIfRequiredPluginsHaveProblems();

        foreach (var unit in sortedUnits)
        {
            ApplyUnit(builder, unit, handlers);
            // A unit that failed in one plugin type keeps running its other types; record what they applied.
            unit.State ??= PluginState.Loaded;
            Catalog.Set(unit.ToInfo());
        }

        ThrowIfRequiredPluginsHaveProblems();
        MoveToEnd(builder, settingsSource);

        // First among the hosted services, so that startup tasks run before any other service starts.
        if (builder.Services.Any(service => service.ServiceType == typeof(PluginStartupRegistration)))
        {
            builder.Services.Insert(0, ServiceDescriptor.Singleton<IHostedService, PluginStartupRunner>());
        }

        builder.Services.AddSingleton(this);
        builder.Services.AddSingleton<IPluginCatalog>(Catalog);
        builder.Services.AddSingleton(new PluginReport([.. _activePlugins.Select(active => active.Plugin)], [.. _verifications]));
    }

    /// <summary>
    /// Adds the plugin settings file as the last configuration source and registers the store that writes it.
    /// </summary>
    private PluginSettingsConfigurationSource? AddSettingsFile(IHostApplicationBuilder builder)
    {
        if (Plugins.Options.SettingsFile is not { } settingsFile)
        {
            return null;
        }

        var path = Path.GetFullPath(settingsFile, builder.Environment.ContentRootPath);
        var source = new PluginSettingsConfigurationSource(path);
        builder.Configuration.Sources.Add(source);
        builder.Services.AddSingleton<IPluginSettingsStore>(_ => new PluginSettingsStore(path));
        return source;
    }

    /// <summary>
    /// Keeps the plugin settings file the last configuration source, even if plugins added sources in phase 1.
    /// </summary>
    private static void MoveToEnd(IHostApplicationBuilder builder, PluginSettingsConfigurationSource? source)
    {
        var sources = builder.Configuration.Sources;
        if (source is not null && sources[^1] != source)
        {
            sources.Remove(source);
            sources.Add(source);
        }
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

        foreach ((var plugin, var handlers) in _activePlugins)
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
        foreach ((var plugin, var _) in _activePlugins)
        {
            PluginLog.PluginLoaded(logger, plugin.Metadata.Id, plugin.Metadata.Version, plugin.PluginType.FullName, plugin.Source);
        }

        foreach (var plugin in Catalog.Plugins)
        {
            PluginLog.PluginStatus(logger, plugin);
        }

        if (app.Services.GetService<IMeterFactory>() is { } meterFactory)
        {
            PluginMetrics.Register(meterFactory, Catalog);
        }
    }

    /// <summary>
    /// Creates one unit per plugin assembly, in the order the sources were added. Plugin folders are verified
    /// and read, but not loaded for execution.
    /// </summary>
    private List<PluginUnit> DiscoverUnits(IHostApplicationBuilder builder)
    {
        if (Verifiers.Count == 0 && Plugins.Any(source => source is not PluginTypeSource))
        {
            PluginLog.PluginVerificationDisabled(Logger);
        }

        var context = new PluginDiscoveryContext(
            builder.Environment.ContentRootPath,
            Verifiers.Count > 0,
            Logger,
            Verify,
            (pluginId, location, reason, exception) => Fail(pluginId, location, PluginState.Failed, reason, exception),
            pluginId => PluginConfiguration.IsDisabled(builder.Configuration, pluginId));

        List<PluginUnit> units = [];
        foreach (var source in Plugins)
        {
            switch (source)
            {
                case PluginTypeSource typeSource:
                    var assembly = typeSource.PluginType.Assembly;
                    PluginUnit? unit = units.FirstOrDefault(unit => unit.Assembly == assembly);
                    if (unit is null)
                    {
                        unit = PluginUnit.FromCode(assembly);
                        units.Add(unit);
                    }

                    unit.PluginTypes.Add(typeSource.PluginType);
                    if (unit.State is null && PluginConfiguration.IsDisabled(builder.Configuration, unit.Id))
                    {
                        unit.State = PluginState.Disabled;
                        unit.Reason = PluginConfiguration.DisabledReason(unit.Id);
                    }

                    break;

                case PluginDirectorySource directorySource:
                    var directory = Path.GetFullPath(directorySource.Path, builder.Environment.ContentRootPath);
                    units.AddRange(DiscoverDirectory(directory, directorySource, builder.Configuration));
                    break;

                case IDiscoveringPluginSource discoveringSource:
                    units.AddRange(discoveringSource.Discover(context));
                    break;
            }
        }

        return units;
    }

    private List<PluginUnit> DiscoverDirectory(string directory, PluginDirectorySource source, IConfiguration configuration)
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
            if (DiscoverFolder(folder, source, configuration) is { } unit)
            {
                units.Add(unit);
            }
        }

        return units;
    }

    private PluginUnit? DiscoverFolder(string folder, PluginDirectorySource source, IConfiguration configuration)
    {
        var name = Path.GetFileName(folder);
        var mainAssemblyFile = $"{name}.dll";
        var mainAssemblyPath = Path.Combine(folder, mainAssemblyFile);
        if (!File.Exists(mainAssemblyPath))
        {
            Fail(
                name,
                folder,
                PluginState.Failed,
                $"The folder contains no '{mainAssemblyFile}'. Each plugin folder must contain an assembly with the folder's name.",
                exception: null);
            return null;
        }

        if (PluginConfiguration.IsDisabled(configuration, name))
        {
            // Read nothing unverified: without verifiers, the metadata can be read without running plugin code.
            return PluginUnit.Disabled(name, folder, "folder", Verifiers.Count == 0 ? TryReadMetadata(mainAssemblyPath) : null);
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
            return PluginUnit.FromFolder(info, folder, new FolderAssemblies(mainAssemblyPath, verifiedFiles));
        }
        catch (Exception exception)
        {
            Fail(name, folder, PluginState.Failed, $"'{mainAssemblyFile}' could not be read: {exception.Message}", exception);
            return null;
        }
    }

    private static PluginMetadata? TryReadMetadata(string mainAssemblyPath)
    {
        try
        {
            return PluginMetadataReader.ReadWithoutLoading(mainAssemblyPath, verifiedContent: null).Metadata;
        }
        catch (Exception exception) when (exception is IOException or BadImageFormatException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Before any plugin is configured: plugins that depend, directly or transitively, on a plugin that is turned off
    /// or failed are not loaded. A chain that starts at a plugin turned off is no failure; other chains are.
    /// </summary>
    private void SkipDependentsOfUnavailable(List<PluginUnit> sortedUnits)
    {
        foreach (var unit in sortedUnits.Where(unit => unit.State == PluginState.Disabled && unit.IsRequired))
        {
            _requiredProblems.Add((
                unit.Id,
                $"Required plugin '{unit.Id}' is turned off in configuration ({PluginConfiguration.EnabledKey(unit.Id)}). "
                    + "Required plugins cannot be turned off.",
                null));
        }

        foreach (var unit in sortedUnits.Where(unit => unit.State is null))
        {
            if (unit.DependsOn.FirstOrDefault(dependency => dependency.Failed) is not { } dependency)
            {
                continue;
            }

            var reason = $"It depends on plugin '{dependency.Id}', which is {Describe(dependency.State)}.";
            if (dependency.State == PluginState.Disabled || dependency.UnavailableBecauseDisabled)
            {
                unit.State = PluginState.DependencyUnavailable;
                unit.Reason = reason;
                unit.UnavailableBecauseDisabled = true;
                Catalog.Set(unit.ToInfo());
                if (unit.IsRequired)
                {
                    _requiredProblems.Add((unit.Id, $"Required plugin '{unit.Id}' cannot start because it depends on {DescribeChain(dependency)}.", null));
                }
            }
            else
            {
                Fail(unit, PluginState.DependencyUnavailable, reason, exception: null);
            }
        }
    }

    /// <summary>
    /// Marks the required plugins and records every required plugin that no source provides.
    /// </summary>
    private void FindRequiredPlugins(List<PluginUnit> units)
    {
        foreach (var unit in units)
        {
            unit.IsRequired = Plugins.GetPolicy(unit.Id)?.Required == true;
        }

        foreach (var pluginId in Plugins.RequiredPluginIds)
        {
            var known = units.Any(unit => string.Equals(unit.Id, pluginId, StringComparison.OrdinalIgnoreCase))
                || Catalog.Find(pluginId) is not null;
            if (!known)
            {
                _requiredProblems.Add((pluginId, $"Required plugin '{pluginId}' was not found. No plugin source provides it.", null));
            }
        }
    }

    /// <summary>
    /// Throws one exception that names every required plugin with a problem.
    /// </summary>
    private void ThrowIfRequiredPluginsHaveProblems()
    {
        if (_requiredProblems.Count == 0)
        {
            return;
        }

        var message = _requiredProblems.Count == 1
            ? _requiredProblems[0].Problem
            : "Required plugins cannot start:" + string.Concat(_requiredProblems.Select(problem => $"{Environment.NewLine}- {problem.Problem}"));
        throw new PluginException(
            message,
            _requiredProblems.Count == 1 ? _requiredProblems[0].PluginId : null,
            _requiredProblems.Count == 1 ? _requiredProblems[0].Exception : null);
    }

    /// <summary>
    /// Describes the chain from a dependency to the plugin that is not available, for example
    /// <c>'B', which depends on 'A', which is disabled</c>.
    /// </summary>
    private static string DescribeChain(PluginUnit dependency)
    {
        List<string> chain = [];
        var current = dependency;
        while (current.State == PluginState.DependencyUnavailable && current.DependsOn.FirstOrDefault(next => next.Failed) is { } next)
        {
            chain.Add($"'{current.Id}', which depends on ");
            current = next;
        }

        return string.Concat(chain) + $"'{current.Id}', which is {Describe(current.State)}";
    }

    /// <summary>
    /// Reads all files of a plugin folder into memory and lets every responsible verifier check them.
    /// </summary>
    /// <returns>The verified files, or <see langword="null"/> if verification failed.</returns>
    private VerifiedFiles? VerifyFolder(string folder, PluginDirectorySource source)
    {
        var name = Path.GetFileName(folder);

        VerifiedFiles verifiedFiles;
        List<PluginFile> files;
        try
        {
            (verifiedFiles, files) = VerifiedFiles.Read(folder);
        }
        catch (IOException exception)
        {
            Fail(name, folder, PluginState.Failed, $"The plugin files could not be read: {exception.Message}", exception);
            return null;
        }

        return Verify(source, name, folder, files, name) ? verifiedFiles : null;
    }

    /// <summary>
    /// Lets every responsible verifier check a plugin's files. Logs and records every result, and reports a failure.
    /// </summary>
    /// <returns><see langword="true"/> if all responsible verifiers passed.</returns>
    private bool Verify(PluginSource source, string name, string location, IReadOnlyList<PluginFile> files, string pluginId)
    {
        var verifiers = Verifiers.Where(verifier => verifier.CanVerify(source)).ToList();
        if (verifiers.Count == 0)
        {
            Fail(
                pluginId,
                location,
                PluginState.VerificationFailed,
                $"No registered plugin verifier can verify plugins from this source ({source}). "
                + "Register one with builder.PluginVerifiers, or remove all verifiers to load plugins without verification.",
                exception: null);
            return false;
        }

        var request = new PluginVerificationRequest(source, name, location, files);
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
                PluginLog.PluginVerificationFailed(Logger, exception, name, location, verifierName, exception.Message);
                _verifications.Add(new PluginVerification(name, location, verifierName, succeeded: false, exception.Message));
                Fail(pluginId, location, PluginState.VerificationFailed, $"Verification by {verifierName} failed: {exception.Message}", exception);
                return false;
            }

            if (!result.Succeeded)
            {
                PluginLog.PluginVerificationFailed(Logger, exception: null, name, location, verifierName, result.Message);
                _verifications.Add(new PluginVerification(name, location, verifierName, succeeded: false, result.Message));
                Fail(pluginId, location, PluginState.VerificationFailed, $"Verification by {verifierName} failed: {result.Message}", exception: null);
                return false;
            }

            PluginLog.PluginVerified(Logger, name, location, verifierName, result.Message);
            _verifications.Add(new PluginVerification(name, location, verifierName, succeeded: true, result.Message));
        }

        return true;
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
            Catalog.Set(unit.ToInfo());
            return;
        }

        if (unit.DependsOn.FirstOrDefault(dependency => dependency.Failed) is { } failedDependency)
        {
            Fail(
                unit,
                PluginState.DependencyUnavailable,
                $"It depends on plugin '{failedDependency.Id}', which is {Describe(failedDependency.State)}.",
                exception: null);
            return;
        }

        if (!unit.IsInCode && !LoadUnit(unit, handlers))
        {
            return;
        }

        foreach (var pluginType in unit.PluginTypes)
        {
            ApplyPlugin(builder, unit, pluginType, handlers);
        }
    }

    /// <summary>
    /// Loads the main assembly of a plugin into its own load context and finds its plugin types.
    /// </summary>
    private bool LoadUnit(PluginUnit unit, List<IPluginHandler> handlers)
    {
        var mainAssemblyFile = $"{unit.Assemblies!.MainAssemblyName}.dll";
        var dependencies = unit.DependsOn.ToDictionary(dependency => dependency.Id, dependency => dependency.Assembly!);

        Type[] exportedTypes;
        try
        {
            unit.Assembly = new PluginLoadContext(unit.Assemblies, dependencies).LoadMainAssembly();
            exportedTypes = unit.Assembly.GetExportedTypes();
        }
        catch (Exception exception)
        {
            Fail(unit, PluginState.Failed, $"'{mainAssemblyFile}' could not be loaded: {exception.Message}", exception);
            return false;
        }

        unit.PluginTypes.AddRange(exportedTypes
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters)
            .Where(type => handlers.Any(handler => handler.CanHandle(type))));

        if (unit.PluginTypes.Count == 0)
        {
            Fail(unit, PluginState.NoHandler, $"'{mainAssemblyFile}' contains no public plugin type that a registered handler can handle.", exception: null);
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
                PluginState.NoHandler,
                $"No plugin handler can handle plugin type '{pluginType}'. Implement {nameof(IPlugin)}, "
                + "or register a handler for it with builder.PluginHandlers.Add<T>().",
                exception: null);
            return;
        }

        if (pluginType.GetConstructor(Type.EmptyTypes) is null)
        {
            Fail(
                unit,
                PluginState.Failed,
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
            Fail(unit, PluginState.Failed, $"Plugin type '{pluginType}' could not be created: {cause.Message}", cause);
            return;
        }

        var context = new PluginContext(
            builder,
            builder.Configuration.GetSection(PluginConfiguration.SettingsSectionPath(unit.Id)),
            unit.Metadata,
            _logBuffer.CreateLogger(pluginType.FullName ?? pluginType.Name));
        var plugin = new PluginDescriptor(pluginType, instance, context, unit.Source);

        // If a handler fails, the services registered for this plugin type are taken back. Configuration sources,
        // builder properties and ConfigureContainer callbacks cannot be taken back.
        var servicesBefore = builder.Services.ToArray();

        foreach (var handler in handlers)
        {
            try
            {
                handler.Apply(plugin, builder);
                if (!unit.AppliedHandlers.Contains(handler.GetType().Name))
                {
                    unit.AppliedHandlers.Add(handler.GetType().Name);
                }
            }
            catch (Exception exception)
            {
                RestoreServices(builder.Services, servicesBefore);
                Fail(
                    unit,
                    PluginState.Failed,
                    $"Phase 1 failed for plugin type '{pluginType}' in handler '{handler.GetType()}': {exception.Message}",
                    exception);
                return;
            }
        }

        _activePlugins.Add(new ActivePlugin(plugin, handlers));
    }

    /// <summary>
    /// A startup task failed. A required plugin stops the application, and so does the fail-fast mode. Otherwise the
    /// plugin becomes failed and the plugins that depend on it, directly or transitively, become unavailable.
    /// </summary>
    public void FailStartup(PluginInfo plugin, string taskName, Exception exception)
    {
        var reason = $"Startup task '{taskName}' failed: {exception.Message}";
        Catalog.Set(plugin with { State = PluginState.Failed, Reason = reason, ErrorMessage = exception.Message });

        var policy = Plugins.GetPolicy(plugin.Id);
        if (plugin.IsRequired)
        {
            throw new PluginException($"Required plugin '{plugin.Id}' ({plugin.Source}): {reason}", plugin.Id, exception);
        }

        if ((policy?.FailureMode ?? Plugins.Options.FailureMode) == PluginFailureMode.FailFast)
        {
            throw new PluginException($"Plugin '{plugin.Id}' ({plugin.Source}): {reason}", plugin.Id, exception);
        }

        bool changed;
        do
        {
            changed = false;
            foreach (var dependent in Catalog.Plugins.Where(candidate => candidate.State == PluginState.Loaded))
            {
                var dependency = dependent.Dependencies
                    .Select(Catalog.Find)
                    .FirstOrDefault(candidate => candidate is not null && candidate.State != PluginState.Loaded);
                if (dependency is null)
                {
                    continue;
                }

                var dependentReason = $"It depends on plugin '{dependency.Id}', which is {Describe(dependency.State)}.";
                Catalog.Set(dependent with { State = PluginState.DependencyUnavailable, Reason = dependentReason });
                if (dependent.IsRequired)
                {
                    throw new PluginException(
                        $"Required plugin '{dependent.Id}' cannot start because it depends on plugin '{dependency.Id}', which is "
                            + $"{Describe(dependency.State)}. {reason}",
                        dependent.Id,
                        exception);
                }

                changed = true;
            }
        }
        while (changed);
    }

    private static void RestoreServices(IServiceCollection services, ServiceDescriptor[] servicesBefore)
    {
        services.Clear();
        foreach (var service in servicesBefore)
        {
            services.Add(service);
        }
    }

    /// <summary>
    /// Reports a failed unit: throws in fail-fast mode, otherwise logs it and records its state,
    /// so that units depending on it are skipped.
    /// </summary>
    private void Fail(PluginUnit unit, PluginState state, string reason, Exception? exception)
    {
        unit.State = state;
        unit.Reason = reason;
        unit.ErrorMessage = exception?.Message;
        Fail(unit.Id, unit.Source, reason, exception, unit.ToInfo());
    }

    /// <summary>
    /// Reports a plugin that failed before it became a unit, for example in verification. Its ID is the name of its
    /// folder or package.
    /// </summary>
    private void Fail(string pluginId, string source, PluginState state, string reason, Exception? exception) =>
        Fail(pluginId, source, reason, exception, new PluginInfo
        {
            Id = pluginId,
            Source = source,
            State = state,
            Reason = reason,
            ErrorMessage = exception?.Message,
        });

    /// <summary>
    /// A required plugin's failure is collected and thrown together with the others. Otherwise the plugin's failure
    /// mode decides: throw now, or log and continue.
    /// </summary>
    private void Fail(string pluginId, string source, string reason, Exception? exception, PluginInfo info)
    {
        var policy = Plugins.GetPolicy(pluginId);
        info = info with { IsRequired = policy?.Required == true };
        Catalog.Set(info);

        if (info.IsRequired)
        {
            _requiredProblems.Add((pluginId, $"Required plugin '{pluginId}' ({source}): {reason}", exception));
            return;
        }

        if ((policy?.FailureMode ?? Plugins.Options.FailureMode) == PluginFailureMode.FailFast)
        {
            var requiredProblems = string.Concat(_requiredProblems.Select(problem => $"{Environment.NewLine}- {problem.Problem}"));
            throw new PluginException(
                $"Plugin '{pluginId}' ({source}): {reason}"
                    + (requiredProblems.Length == 0 ? "" : $"{Environment.NewLine}Required plugins cannot start either:{requiredProblems}"),
                pluginId,
                exception);
        }

        PluginLog.PluginSkipped(Logger, exception, pluginId, source, reason);
    }

    private static string Describe(PluginState? state) => state switch
    {
        PluginState.Disabled => "disabled",
        PluginState.DependencyUnavailable => "not loaded because of its own dependencies",
        PluginState.VerificationFailed => "not loaded because it failed verification",
        PluginState.NoHandler => "not loaded because no handler can handle it",
        _ => "not loaded because it failed",
    };

    private sealed record ActivePlugin(PluginDescriptor Plugin, List<IPluginHandler> Handlers);
}
