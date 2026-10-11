using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;

namespace Plugwright;

/// <summary>
/// The plugins of a host application, available as <c>builder.Plugins</c>.
/// </summary>
/// <remarks>
/// Adding plugins only records types and directories. Nothing is loaded or run until <c>builder.ApplyPlugins()</c>;
/// then the plugin handlers decide what happens with each plugin. Plugins cannot be added after that call.
/// The collection enumerates the added sources in the order they were added.
/// </remarks>
public sealed class PluginCollection : IReadOnlyCollection<PluginSource>
{
    private readonly PluginSystem _system;
    private readonly List<PluginSource> _sources = [];
    private readonly Dictionary<string, PluginPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);

    internal PluginCollection(PluginSystem system)
    {
        _system = system;
    }

    /// <summary>
    /// Gets the options of the plugin system.
    /// </summary>
    public PluginOptions Options { get; } = new();

    /// <summary>
    /// Gets the number of sources added.
    /// </summary>
    public int Count => _sources.Count;

    /// <summary>
    /// Adds a plugin type from the application itself or from a referenced assembly.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin type. Usually it implements <see cref="IPlugin"/>.</typeparam>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">The type has already been added.</exception>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.Add&lt;HeartbeatPlugin&gt;();
    /// </code>
    /// </example>
    public PluginCollection Add<TPlugin>()
        where TPlugin : class, new()
    {
        return Add(typeof(TPlugin));
    }

    /// <summary>
    /// Adds a plugin type from the application itself or from a referenced assembly, and sets the policy of its plugin.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin type. Usually it implements <see cref="IPlugin"/>.</typeparam>
    /// <param name="configure">Sets the policy, for example whether the plugin is required.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">The type has already been added.</exception>
    /// <remarks>
    /// The policy belongs to the plugin ID, which all plugin types of one assembly share. See
    /// <see cref="Configure(string, Action{PluginPolicy})"/>.
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.Add&lt;OrdersPlugin&gt;(policy =&gt; policy.IsRequired = true);
    /// builder.Plugins.Add&lt;ReportsPlugin&gt;(policy =&gt; policy.FailureMode = PluginFailureMode.SkipAndLog);
    /// </code>
    /// </example>
    public PluginCollection Add<TPlugin>(Action<PluginPolicy> configure)
        where TPlugin : class, new()
    {
        return Add(typeof(TPlugin), configure);
    }

    /// <summary>
    /// Adds a plugin type from the application itself or from a referenced assembly, and sets the policy of its plugin.
    /// </summary>
    /// <param name="pluginType">The plugin type. It must be a non-abstract class with a public parameterless constructor.</param>
    /// <param name="configure">Sets the policy, for example whether the plugin is required.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">The type is not a valid plugin type or has already been added.</exception>
    public PluginCollection Add(Type pluginType, Action<PluginPolicy> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Add(pluginType);
        return Configure(PluginMetadataReader.Read(pluginType.Assembly).Metadata.Id, configure);
    }

    /// <summary>
    /// Sets the policy of a plugin by its ID, for plugins from any source, also directories and packages.
    /// </summary>
    /// <param name="pluginId">The plugin ID, compared without case.</param>
    /// <param name="configure">Changes the policy. Several calls for one ID change the same policy.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.AddFromDirectory("plugins");
    /// builder.Plugins.Configure("Acme.Reports", policy =&gt; policy.FailureMode = PluginFailureMode.SkipAndLog);
    /// </code>
    /// </example>
    public PluginCollection Configure(string pluginId, Action<PluginPolicy> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(configure);
        _system.ThrowIfApplied("Plugin policies cannot be changed");

        if (!_policies.TryGetValue(pluginId, out var policy))
        {
            _policies[pluginId] = policy = new PluginPolicy();
        }

        configure(policy);
        return this;
    }

    /// <summary>
    /// Marks a plugin as required: the application does not start without it. Works for plugins from any source.
    /// </summary>
    /// <param name="pluginId">The plugin ID, compared without case.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <remarks>
    /// <c>builder.ApplyPlugins()</c> throws a <see cref="PluginException"/> if no source provides the plugin, if it is
    /// turned off in configuration, or if it cannot be loaded. See <see cref="PluginPolicy.IsRequired"/>.
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.AddFromDirectory("plugins");
    /// builder.Plugins.Require("Acme.Billing");
    /// </code>
    /// </example>
    public PluginCollection Require(string pluginId) => Configure(pluginId, policy => policy.IsRequired = true);

    /// <summary>
    /// Shares an assembly of the host with all plugins from directories and packages: they use the host's copy, so
    /// that its types are the same in the host and in every plugin.
    /// </summary>
    /// <param name="assembly">An assembly the host has loaded, usually <c>typeof(SomeType).Assembly</c>.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">The assembly is not in the host's default load context.</exception>
    /// <remarks>
    /// <para>
    /// Share the contracts of your application, UI libraries and frameworks whose types plugins exchange with the host,
    /// such as Entity Framework Core. The host's version is used even if a plugin ships another version; if the
    /// plugin was compiled against a newer one, it can fail at runtime with a <see cref="MissingMethodException"/>.
    /// </para>
    /// <para>
    /// The plugin contracts, <c>Microsoft.Extensions.*</c> and <c>Microsoft.AspNetCore.*</c> are always shared.
    /// Plugins added in code use the host's assemblies anyway.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.ShareAssembly(typeof(INavigationItem).Assembly);
    /// </code>
    /// </example>
    public PluginCollection ShareAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _system.ThrowIfApplied("Assemblies cannot be shared");

        if (AssemblyLoadContext.GetLoadContext(assembly) != AssemblyLoadContext.Default)
        {
            throw new ArgumentException(
                $"Assembly '{assembly.GetName().Name}' is not in the host's default load context. "
                + "Only assemblies of the host can be shared with plugins.",
                nameof(assembly));
        }

        SharedAssemblies.AddName(assembly.GetName().Name!);
        return this;
    }

    /// <summary>
    /// Shares all assemblies of the host whose names start with a prefix with all plugins from directories and
    /// packages, like <see cref="ShareAssembly"/>.
    /// </summary>
    /// <param name="prefix">
    /// The start of the assembly names, compared without case and only at a dot: <c>Microsoft.EntityFrameworkCore</c>
    /// shares <c>Microsoft.EntityFrameworkCore</c> and <c>Microsoft.EntityFrameworkCore.SqlServer</c>, but not
    /// <c>Microsoft.EntityFrameworkCoreX</c>.
    /// </param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">The prefix is empty, or starts or ends with a dot.</exception>
    /// <remarks>
    /// An assembly with a matching name that the host does not have is not shared: the plugin loads its own copy.
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.ShareAssemblies("Microsoft.EntityFrameworkCore");
    /// </code>
    /// </example>
    public PluginCollection ShareAssemblies(string prefix)
    {
        AssemblyNamePrefix.ThrowIfInvalid(prefix, nameof(prefix));
        _system.ThrowIfApplied("Assemblies cannot be shared");

        SharedAssemblies.AddPrefix(prefix);
        return this;
    }

    /// <summary>
    /// Gets the assemblies that plugins from directories and packages take from the host.
    /// </summary>
    internal SharedAssemblies SharedAssemblies { get; } = new();

    /// <summary>
    /// Returns the policy of a plugin, or <see langword="null"/> if the application set none.
    /// </summary>
    internal PluginPolicy? GetPolicy(string pluginId) => _policies.GetValueOrDefault(pluginId);

    /// <summary>
    /// Gets the IDs of all required plugins.
    /// </summary>
    internal IEnumerable<string> RequiredPluginIds => _policies.Where(entry => entry.Value.IsRequired).Select(entry => entry.Key);

    /// <summary>
    /// Adds a plugin type from the application itself or from a referenced assembly.
    /// </summary>
    /// <param name="pluginType">The plugin type. It must be a non-abstract class with a public parameterless constructor.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">The type is not a valid plugin type or has already been added.</exception>
    public PluginCollection Add(Type pluginType)
    {
        ArgumentNullException.ThrowIfNull(pluginType);
        _system.ThrowIfApplied("Plugins cannot be added");

        if (!pluginType.IsClass || pluginType.IsAbstract || pluginType.ContainsGenericParameters)
        {
            throw new ArgumentException(
                $"Plugin type '{pluginType}' must be a non-abstract, non-generic class.", nameof(pluginType));
        }

        if (pluginType.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new ArgumentException(
                $"Plugin type '{pluginType}' must have a public parameterless constructor. "
                + "Plugins are created before the service container exists, so they cannot receive services through their constructor.",
                nameof(pluginType));
        }

        if (_sources.OfType<PluginTypeSource>().Any(source => source.PluginType == pluginType))
        {
            throw new ArgumentException($"Plugin type '{pluginType}' has already been added.", nameof(pluginType));
        }

        _sources.Add(new PluginTypeSource(pluginType));
        return this;
    }

    /// <summary>
    /// Adds all plugins from a plugin directory. Each plugin lives in its own subfolder and is loaded
    /// into its own <see cref="System.Runtime.Loader.AssemblyLoadContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The directory is read when <c>builder.ApplyPlugins()</c> runs. Every subfolder must contain an assembly with the
    /// folder's name, for example <c>plugins/Acme.Heartbeat/Acme.Heartbeat.dll</c>, together with its dependencies.
    /// Every public type in that assembly that a registered handler can handle is a plugin.
    /// </para>
    /// <para>
    /// A relative path is resolved against the host's content root, <c>builder.Environment.ContentRootPath</c>.
    /// If the directory does not exist, a warning is logged and no plugins are loaded from it.
    /// Assemblies directly in the directory, outside a subfolder, are ignored with a warning.
    /// </para>
    /// <para>
    /// Plugins run with full trust inside the host process.
    /// </para>
    /// </remarks>
    /// <param name="path">The plugin directory, absolute or relative to the content root.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">The path is empty.</exception>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.AddFromDirectory("plugins");
    /// </code>
    /// </example>
    public PluginCollection AddFromDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _system.ThrowIfApplied("Plugins cannot be added");

        _sources.Add(new PluginDirectorySource(path));
        return this;
    }

    /// <summary>
    /// Adds all plugin types of an assembly of the application or one it references: every public, non-abstract
    /// class that a registered handler can handle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The assembly's plugins count as plugins added in code: they run in the host's load context, are never verified,
    /// and can only depend on other plugins added in code. Their types are found when <c>builder.ApplyPlugins()</c> runs.
    /// </para>
    /// <para>
    /// An assembly without plugin types is a plugin failure. Types also added with <see cref="Add{TPlugin}()"/> are
    /// applied once.
    /// </para>
    /// </remarks>
    /// <param name="assembly">The assembly, usually <c>typeof(SomePlugin).Assembly</c>.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">
    /// The assembly is not in the host's default load context, or it has already been added.
    /// </exception>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.AddFromAssembly(typeof(OrdersPlugin).Assembly);
    /// </code>
    /// </example>
    public PluginCollection AddFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _system.ThrowIfApplied("Plugins cannot be added");

        if (AssemblyLoadContext.GetLoadContext(assembly) != AssemblyLoadContext.Default)
        {
            throw new ArgumentException(
                $"Assembly '{assembly.GetName().Name}' is not in the host's default load context. "
                + "Load plugins from other places with AddFromDirectory.",
                nameof(assembly));
        }

        if (_sources.OfType<PluginAssemblySource>().Any(source => source.Assembly == assembly))
        {
            throw new ArgumentException($"Assembly '{assembly.GetName().Name}' has already been added.", nameof(assembly));
        }

        _sources.Add(new PluginAssemblySource(assembly));
        return this;
    }

    /// <summary>
    /// Adds the plugin types of all assemblies the application references whose names start with a prefix, as with
    /// <see cref="AddFromAssembly"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The referenced assemblies are read from the application's <c>.deps.json</c> file when <c>builder.ApplyPlugins()</c>
    /// runs, and matching ones are loaded. That covers project and package references, including assemblies the
    /// application does not use otherwise. Matching assemblies without plugin types, such as contract libraries, are
    /// skipped.
    /// </para>
    /// <para>
    /// The application needs its <c>.deps.json</c> file, which <c>dotnet build</c> and <c>dotnet publish</c> create.
    /// Without it, <c>builder.ApplyPlugins()</c> throws. Trimming can remove plugin types that nothing references; use
    /// <see cref="Add{TPlugin}()"/> in trimmed applications.
    /// </para>
    /// </remarks>
    /// <param name="prefix">
    /// The start of the assembly names, compared without case and only at a dot: <c>Acme.Plugins</c> matches
    /// <c>Acme.Plugins</c> and <c>Acme.Plugins.Orders</c>, but not <c>Acme.PluginsX</c>.
    /// </param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">The prefix is empty, or starts or ends with a dot.</exception>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.AddFromReferencedAssemblies("Acme.Plugins");
    /// </code>
    /// </example>
    public PluginCollection AddFromReferencedAssemblies(string prefix)
    {
        AssemblyNamePrefix.ThrowIfInvalid(prefix, nameof(prefix));
        _system.ThrowIfApplied("Plugins cannot be added");

        _sources.Add(new PluginReferencedAssembliesSource(prefix));
        return this;
    }

    /// <summary>
    /// Sets up the plugin system from configuration: the failure mode and the plugin sources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The section has these keys, all optional:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>FailureMode</c>: <c>FailFast</c> or <c>SkipAndLog</c>, see <see cref="PluginOptions.FailureMode"/>.</description></item>
    /// <item><description><c>SettingsFile</c>: the plugin settings file, empty to turn it off, see <see cref="PluginOptions.SettingsFile"/>.</description></item>
    /// <item><description><c>Directories</c>: plugin directories, see <see cref="AddFromDirectory"/>.</description></item>
    /// <item><description><c>Packages</c>: package folders; needs Plugwright.NuGet and <c>builder.Plugins.AddNuGetSupport()</c>.</description></item>
    /// <item><description><c>Plugins</c> and <c>Verification</c>: read elsewhere and ignored here.</description></item>
    /// </list>
    /// <para>
    /// Any other key is an error, so that a typo does not go unnoticed. Plugins added in code and sources from configuration
    /// can be combined. Protect the configuration like the plugin directory: whoever can change it decides which plugins run.
    /// </para>
    /// </remarks>
    /// <param name="section">The section, usually <c>builder.Configuration.GetSection("Plugwright")</c>.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// <c>builder.ApplyPlugins()</c> has already been called, or the configuration has an unknown key or an invalid value.
    /// </exception>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.AddFromConfiguration(builder.Configuration.GetSection("Plugwright"));
    /// </code>
    /// <code language="json">
    /// {
    ///   "Plugwright": {
    ///     "FailureMode": "SkipAndLog",
    ///     "Directories": [ "plugins" ]
    ///   }
    /// }
    /// </code>
    /// </example>
    public PluginCollection AddFromConfiguration(IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        _system.ThrowIfApplied("Plugins cannot be added");

        var readers = _system.ConfigurationReaders.Sources;
        string[] ownKeys = ["FailureMode", "SettingsFile", "Directories", "Plugins", "Verification"];
        foreach (var child in section.GetChildren())
        {
            if (!ownKeys.Contains(child.Key, StringComparer.OrdinalIgnoreCase) && !readers.ContainsKey(child.Key))
            {
                throw PluginConfigurationReaders.UnknownKey(child, [.. ownKeys, .. readers.Keys]);
            }
        }

        if (section["FailureMode"] is { } failureMode)
        {
            Options.FailureMode = Enum.TryParse<PluginFailureMode>(failureMode, ignoreCase: true, out var mode) && Enum.IsDefined(mode)
                ? mode
                : throw new InvalidOperationException(
                    $"The configuration '{section.Path}:FailureMode' has the invalid value '{failureMode}'. "
                    + $"Valid values are: {string.Join(", ", Enum.GetNames<PluginFailureMode>())}.");
        }

        if (section.GetSection("SettingsFile") is { Value: { } settingsFile })
        {
            Options.SettingsFile = settingsFile.Length == 0 ? null : settingsFile;
        }

        foreach (var directory in section.GetSection("Directories").GetChildren())
        {
            AddFromDirectory(directory.Value
                ?? throw new InvalidOperationException($"The configuration '{directory.Path}' must be the path of a plugin directory."));
        }

        foreach ((var key, var read) in readers)
        {
            if (section.GetSection(key) is { } sourceSection && sourceSection.Exists())
            {
                read(this, sourceSection);
            }
        }

        return this;
    }

    /// <summary>
    /// Gets the configuration readers, so that packages such as Plugwright.NuGet can register theirs.
    /// </summary>
    internal PluginConfigurationReaders ConfigurationReaders => _system.ConfigurationReaders;

    /// <summary>
    /// Adds a source of another package, such as the package source of Plugwright.NuGet.
    /// </summary>
    internal PluginCollection AddSource(PluginSource source)
    {
        _system.ThrowIfApplied("Plugins cannot be added");
        _sources.Add(source);
        return this;
    }

    /// <summary>
    /// Returns the added sources in the order they were added.
    /// </summary>
    /// <returns>An enumerator over the sources.</returns>
    public IEnumerator<PluginSource> GetEnumerator() => _sources.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
