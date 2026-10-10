using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Plugwright;

/// <summary>
/// Processes every plugin that implements <see cref="IBlazorPlugin"/>: makes its Razor components known to the router
/// and to <c>MapRazorComponents</c>.
/// </summary>
/// <remarks>
/// The handler registers <see cref="IPluginComponentAssemblies"/> with the assemblies of all active Blazor plugins.
/// It is registered even if no Blazor plugin is active, so a router can always inject it. The handler does not need MVC
/// or <see cref="WebPluginHandler"/>.
/// </remarks>
/// <example>
/// <code language="csharp">
/// builder.Services.AddRazorComponents().AddInteractiveServerComponents();
/// builder.Plugins.Add&lt;OrdersUiPlugin&gt;();
/// builder.PluginHandlers.Add&lt;BlazorPluginHandler&gt;();
/// builder.ApplyPlugins();
///
/// var app = builder.Build();
/// app.UseAntiforgery();
/// app.UsePlugins();
/// app.MapRazorComponents&lt;App&gt;()
///    .AddInteractiveServerRenderMode()
///    .AddPluginAssemblies(app);
/// </code>
/// </example>
public sealed class BlazorPluginHandler : PluginHandler<IBlazorPlugin>, IPluginHandlerSetup
{
    void IPluginHandlerSetup.Setup(IHostApplicationBuilder builder) =>
        builder.Services.AddSingleton<IPluginComponentAssemblies>(
            services => new PluginComponentAssemblies(services.GetRequiredService<PluginReport>()));

    /// <inheritdoc/>
    /// <remarks>
    /// Nothing to do: the component assemblies are collected from the active plugins once the host is built, so a
    /// plugin that fails later in phase 1 does not contribute pages.
    /// </remarks>
    protected override void Apply(IBlazorPlugin instance, PluginDescriptor plugin, IHostApplicationBuilder builder)
    {
    }
}
