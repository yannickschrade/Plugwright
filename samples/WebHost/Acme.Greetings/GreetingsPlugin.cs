using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Plugwright;

namespace Acme.Greetings;

#region plugin
/// <summary>
/// Registers a service, maps an endpoint and adds middleware. The controller in this assembly is found automatically.
/// </summary>
public sealed class GreetingsPlugin : IPlugin, IWebPlugin
{
    // Phase 1: register services, as every plugin does.
    public void Configure(PluginContext context) =>
        context.Builder.Services.AddSingleton<IGreeter>(new Greeter(context.Configuration["Salutation"] ?? "Hello"));

    // Phase 2: middleware, at the position of app.UsePlugins().
    public void ConfigurePipeline(IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.Headers.Append("X-Greeted-By", "Acme.Greetings");
            return next(context);
        });

    // Phase 2: endpoints.
    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/greetings/{name}", (string name, IGreeter greeter) => greeter.Greet(name));
}
#endregion
