namespace Plugwright.AspNetCore;

/// <summary>
/// Marks a plugin whose assembly contains Razor components, such as routable pages, for a Blazor host.
/// Processed by <c>BlazorPluginHandler</c> from Plugwright.AspNetCore.
/// </summary>
/// <remarks>
/// The handler makes the plugin's assembly known to the host, so that the router and <c>MapRazorComponents</c> find its
/// pages. Navigation entries, menus and similar extension points are contracts of the host application; register
/// them as services in <see cref="IPlugin.Configure"/>.
/// </remarks>
/// <example>
/// <code language="csharp">
/// public sealed class OrdersUiPlugin : IPlugin, IBlazorPlugin
/// {
///     public void Configure(PluginContext context) =&gt;
///         context.Builder.Services.AddSingleton&lt;INavigationItem&gt;(new NavigationItem("Orders", "orders"));
/// }
/// </code>
/// </example>
// An empty marker interface instead of an attribute (CA1040): handlers select plugins by the interfaces they
// implement (PluginHandler<T>), so an interface lets BlazorPluginHandler work like every other handler.
public interface IBlazorPlugin
{
}
