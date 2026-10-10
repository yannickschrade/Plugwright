using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Plugwright.AspNetCore;

/// <summary>
/// A plugin for an ASP.NET Core host: brings MVC controllers, endpoints and middleware.
/// Processed by <c>WebPluginHandler</c> from Plugwright.AspNetCore.
/// </summary>
/// <remarks>
/// <para>
/// Controllers in the plugin's assembly are found without further code: the handler adds the assembly as an MVC
/// application part. Both methods are optional; implement only what the plugin needs.
/// </para>
/// <para>
/// Services are registered as usual in <see cref="IPlugin.Configure"/>; a plugin class can implement both interfaces.
/// The plugin system creates one instance per plugin class, so fields set in <see cref="IPlugin.Configure"/> are still
/// there in <see cref="ConfigurePipeline"/> and <see cref="MapEndpoints"/>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// public sealed class OrdersPlugin : IPlugin, IWebPlugin
/// {
///     public void Configure(PluginContext context) =&gt;
///         context.Builder.Services.AddSingleton&lt;OrderService&gt;();
///
///     public void MapEndpoints(IEndpointRouteBuilder endpoints) =&gt;
///         endpoints.MapGet("/orders", (OrderService orders) =&gt; orders.GetAll());
/// }
/// </code>
/// </example>
public interface IWebPlugin
{
    /// <summary>
    /// Phase 2: adds middleware. Called by <c>app.UsePlugins()</c>, so the middleware runs at the position of that call
    /// in the request pipeline. The default implementation adds nothing.
    /// </summary>
    /// <param name="app">The application's request pipeline.</param>
    void ConfigurePipeline(IApplicationBuilder app)
    {
    }

    /// <summary>
    /// Phase 2: maps endpoints such as minimal APIs or SignalR hubs. Called by <c>app.UsePlugins()</c>, after
    /// <see cref="ConfigurePipeline"/> of the same plugin. The default implementation maps nothing.
    /// </summary>
    /// <param name="endpoints">The application's endpoint route builder.</param>
    void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
