using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Plugwright.AspNetCore;

namespace Plugwright.Tests.Plugins;

/// <summary>
/// A web plugin that does not implement <see cref="IPlugin"/>: only the web handler processes it.
/// </summary>
public sealed class EndpointOnlyPlugin : IWebPlugin
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/endpoint-only", () => "endpoint only");
}

/// <summary>
/// Registers a service in phase 1 that the host uses in its own endpoint.
/// </summary>
public sealed class ClockPlugin : IPlugin, IWebPlugin
{
    public void Configure(PluginContext context) => context.Builder.Services.AddSingleton(new PluginClock("12:00"));

    public void ConfigurePipeline(IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.Headers.Append("X-Clock", "plugin");
            return next(context);
        });
}

public sealed record PluginClock(string Time);

public sealed class UiPlugin : IBlazorPlugin
{
}
