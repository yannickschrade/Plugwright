using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Plugwright;

/// <summary>
/// Everything a plugin gets when it is configured: the host builder, its own settings section, its metadata and a logger.
/// </summary>
/// <remarks>
/// New members may be added to this class in later versions. This lets the plugin contract grow
/// without changing <see cref="IPlugin"/>.
/// </remarks>
public sealed class PluginContext
{
    /// <summary>
    /// Creates a plugin context.
    /// </summary>
    /// <remarks>
    /// The plugin system creates the context for you. Use this constructor to call
    /// <see cref="IPlugin.Configure"/> from a unit test.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configuration">The plugin's settings section.</param>
    /// <param name="metadata">The plugin's metadata.</param>
    /// <param name="logger">The logger for the plugin.</param>
    /// <example>
    /// <code language="csharp">
    /// var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
    /// var context = new PluginContext(
    ///     builder,
    ///     new ConfigurationBuilder().Build(),
    ///     new PluginMetadata("Acme.Heartbeat", "1.0.0"),
    ///     NullLogger.Instance);
    ///
    /// new HeartbeatPlugin().Configure(context);
    /// </code>
    /// </example>
    public PluginContext(IHostApplicationBuilder builder, IConfiguration configuration, PluginMetadata metadata, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(logger);

        Builder = builder;
        Configuration = configuration;
        Metadata = metadata;
        Logger = logger;
    }

    /// <summary>
    /// Gets the host application builder. Use it to register services, configuration, logging and metrics.
    /// </summary>
    public IHostApplicationBuilder Builder { get; }

    /// <summary>
    /// Gets the plugin's own settings section, <c>Plugwright:Plugins:{PluginId}:Settings</c>.
    /// </summary>
    /// <remarks>
    /// Bind it with the options pattern. The section is empty, not <see langword="null"/>, when no settings exist.
    /// All plugin classes in one assembly share the same section.
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// context.Builder.Services
    ///     .AddOptions&lt;HeartbeatOptions&gt;()
    ///     .Bind(context.Configuration)
    ///     .ValidateOnStart();
    /// </code>
    /// </example>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// Gets the plugin's identity and version.
    /// </summary>
    public PluginMetadata Metadata { get; }

    /// <summary>
    /// Gets a logger for use inside <see cref="IPlugin.Configure"/>.
    /// </summary>
    /// <remarks>
    /// The logging system is not available before the host is built. Entries written to this logger are
    /// buffered and written to the configured logging providers when the host application calls <c>UsePlugins()</c>.
    /// For logging at runtime, inject <see cref="ILogger{TCategoryName}"/> into your services as usual.
    /// </remarks>
    public ILogger Logger { get; }

    /// <summary>
    /// Gets the catalog the plugin system fills while it applies plugins, or <see langword="null"/> for a context created
    /// in a unit test.
    /// </summary>
    internal IPluginCatalog? Catalog { get; init; }

    /// <summary>
    /// Returns whether a plugin is loaded, for example a plugin this plugin depends on optionally.
    /// </summary>
    /// <param name="pluginId">The plugin ID, compared without case.</param>
    /// <returns>
    /// <see langword="true"/> if the plugin is loaded; <see langword="false"/> if it is missing, turned off, failed, or not
    /// processed yet.
    /// </returns>
    /// <remarks>
    /// Plugins a plugin depends on, also optionally, are always processed before it, so the answer is final for them.
    /// For other plugins it can change later in the start. A context created with the public constructor, as in a unit
    /// test, knows no plugins and returns <see langword="false"/>.
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// public void Configure(PluginContext context)
    /// {
    ///     if (context.IsPluginLoaded("Acme.Logging"))
    ///     {
    ///         context.Builder.Services.AddSingleton&lt;IAuditSink, LoggingAuditSink&gt;();
    ///     }
    /// }
    /// </code>
    /// </example>
    public bool IsPluginLoaded(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        return Catalog?.Find(pluginId)?.State == PluginState.Loaded;
    }

    /// <summary>
    /// Adds an asynchronous startup task for this plugin. It runs after the host is built and before the application
    /// starts, see <see cref="IPluginStartup"/>.
    /// </summary>
    /// <typeparam name="TStartup">The task. It is created in its own service scope and can receive services.</typeparam>
    /// <returns>This context, for chaining.</returns>
    /// <example>
    /// <code language="csharp">
    /// public void Configure(PluginContext context)
    /// {
    ///     context.Builder.Services.AddDbContext&lt;OrdersDbContext&gt;();
    ///     context.AddStartup&lt;MigrateOrdersStartup&gt;();
    /// }
    /// </code>
    /// </example>
    public PluginContext AddStartup<TStartup>()
        where TStartup : class, IPluginStartup
    {
        Builder.Services.TryAddScoped<TStartup>();
        Builder.Services.AddSingleton(new PluginStartupRegistration(Metadata.Id, typeof(TStartup)));
        return this;
    }
}
