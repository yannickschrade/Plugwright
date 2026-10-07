namespace Plugwright;

/// <summary>
/// A plugin that extends the host application through dependency injection.
/// </summary>
/// <remarks>
/// <para>
/// The plugin system creates one instance of every plugin with its parameterless constructor and calls
/// <see cref="Configure"/> once, before the host is built. At that point the service container does not exist yet,
/// so a plugin cannot receive services through its constructor. It registers services instead, and runtime
/// behavior comes from those services, for example an <c>IHostedService</c>.
/// </para>
/// <para>
/// Plugins run with full trust inside the host process.
/// </para>
/// </remarks>
/// <example>
/// A plugin that adds a hosted service and binds its own settings:
/// <code language="csharp">
/// public sealed class HeartbeatPlugin : IPlugin
/// {
///     public void Configure(PluginContext context)
///     {
///         context.Builder.Services
///             .AddOptions&lt;HeartbeatOptions&gt;()
///             .Bind(context.Configuration);
///
///         context.Builder.Services.AddHostedService&lt;HeartbeatService&gt;();
///     }
/// }
/// </code>
/// </example>
public interface IPlugin
{
    /// <summary>
    /// Registers the plugin's services. Called once, before the host is built.
    /// </summary>
    /// <param name="context">The builder, the plugin's settings section, its metadata and a logger.</param>
    void Configure(PluginContext context);
}
