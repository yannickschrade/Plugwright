using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Plugwright.AspNetCore;

/// <summary>
/// Renders a <c>&lt;link rel="stylesheet"&gt;</c> for the scoped CSS of every plugin loaded from a directory or a
/// package. Place it in the <c>&lt;head&gt;</c> of <c>App.razor</c>, next to the application's own
/// <c>{AssemblyName}.styles.css</c>.
/// </summary>
/// <remarks>
/// The scoped CSS of plugins that the application references, or adds in code, is part of the application's own bundle
/// and is not linked again. Requires <see cref="BlazorPluginHandler"/> and <c>app.MapPluginStaticAssets()</c>.
/// </remarks>
/// <example>
/// <code language="razor">
/// &lt;head&gt;
///     &lt;link rel="stylesheet" href="@Assets["MyApp.styles.css"]" /&gt;
///     &lt;PluginStyleSheets /&gt;
///     &lt;HeadOutlet /&gt;
/// &lt;/head&gt;
/// </code>
/// </example>
public sealed class PluginStyleSheets : ComponentBase
{
    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var staticAssets = Services.GetService(typeof(PluginStaticAssets)) as PluginStaticAssets
            ?? throw new InvalidOperationException(
                "<PluginStyleSheets /> needs the Blazor plugin handler. Register it with builder.PluginHandlers.Add<BlazorPluginHandler>().");

        foreach (var styleSheet in staticAssets.StyleSheets)
        {
            builder.OpenElement(0, "link");
            builder.AddAttribute(1, "rel", "stylesheet");
            builder.AddAttribute(2, "href", styleSheet);
            builder.CloseElement();
        }
    }
}
