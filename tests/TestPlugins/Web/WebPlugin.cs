using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Plugwright.TestPlugins.Web;

// --8<-- [start:plugin]
/// <summary>
/// Implements all three plugin contracts. The host does not know this assembly; it is loaded from a plugin directory.
/// </summary>
public sealed class WebPlugin : IPlugin, IWebPlugin, IBlazorPlugin
{
    private string _greeting = "";

    public void Configure(PluginContext context)
    {
        _greeting = context.Configuration["Greeting"] ?? "Hello from the web plugin";
        context.Builder.Services.AddSingleton(new WebGreeting(_greeting));
    }

    public void ConfigurePipeline(IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.Headers.Append("X-Pipeline", "plugin");
            return next(context);
        });

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/web-plugin/endpoint", (WebGreeting greeting) => $"endpoint: {greeting.Text}, instance: {_greeting}");
}

// --8<-- [end:plugin]

public sealed record WebGreeting(string Text);

[ApiController]
[Route("web-plugin/controller")]
public sealed class WebPluginController(WebGreeting greeting) : ControllerBase
{
    [HttpGet]
    public string Get() => $"controller: {greeting.Text}";
}
