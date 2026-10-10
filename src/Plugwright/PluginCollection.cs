using System.Collections;
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
    /// builder.Plugins.Add&lt;OrdersPlugin&gt;(policy =&gt; policy.Required = true);
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
    /// turned off in configuration, or if it cannot be loaded. See <see cref="PluginPolicy.Required"/>.
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.AddFromDirectory("plugins");
    /// builder.Plugins.Require("Acme.Billing");
    /// </code>
    /// </example>
    public PluginCollection Require(string pluginId) => Configure(pluginId, policy => policy.Required = true);

    /// <summary>
    /// Returns the policy of a plugin, or <see langword="null"/> if the application set none.
    /// </summary>
    internal PluginPolicy? GetPolicy(string pluginId) => _policies.GetValueOrDefault(pluginId);

    /// <summary>
    /// Gets the IDs of all required plugins.
    /// </summary>
    internal IEnumerable<string> RequiredPluginIds => _policies.Where(entry => entry.Value.Required).Select(entry => entry.Key);

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

        foreach (var (key, read) in readers)
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
