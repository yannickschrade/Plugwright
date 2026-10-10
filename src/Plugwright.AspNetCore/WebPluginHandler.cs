using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Plugwright.AspNetCore;

/// <summary>
/// Processes every plugin that implements <see cref="IWebPlugin"/>: makes its MVC controllers discoverable and adds its
/// middleware and endpoints.
/// </summary>
/// <remarks>
/// <para>
/// Phase 1 adds the plugin's assembly as an MVC application part. The handler does not call <c>AddControllers()</c>
/// itself; controllers are found as soon as the host uses MVC, whether it calls <c>AddControllers()</c> before or after
/// <c>builder.ApplyPlugins()</c>.
/// </para>
/// <para>
/// Phase 2 calls <see cref="IWebPlugin.ConfigurePipeline"/> and then <see cref="IWebPlugin.MapEndpoints"/> of each
/// plugin, in dependency order. Middleware therefore runs at the position of <c>app.UsePlugins()</c>. The host must be a
/// web application, such as one built by <c>WebApplication.CreateBuilder()</c>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var builder = WebApplication.CreateBuilder(args);
/// builder.Services.AddControllers();
/// builder.Plugins.Add&lt;OrdersPlugin&gt;();
/// builder.PluginHandlers.Add&lt;WebPluginHandler&gt;();
/// builder.ApplyPlugins();
///
/// var app = builder.Build();
/// app.UsePlugins();     // plugin middleware runs here
/// app.MapControllers();
/// app.Run();
/// </code>
/// </example>
public sealed class WebPluginHandler : PluginHandler<IWebPlugin>
{
    /// <inheritdoc/>
    protected override void Apply(IWebPlugin instance, PluginDescriptor plugin, IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(builder);

        AddParts(GetOrAddApplicationPartManager(builder), plugin.PluginType.Assembly);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The host is not a web application.</exception>
    protected override void Use(IWebPlugin instance, PluginDescriptor plugin, IHost app)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(plugin);

        if (app is not IApplicationBuilder pipeline || app is not IEndpointRouteBuilder endpoints)
        {
            throw new InvalidOperationException(
                $"Plugin type '{plugin.PluginType}' is a web plugin, but the host '{app.GetType()}' is not a web application. "
                + "Web plugins need a host that is an IApplicationBuilder and an IEndpointRouteBuilder, "
                + "such as one built by WebApplication.CreateBuilder().");
        }

        instance.ConfigurePipeline(pipeline);
        instance.MapEndpoints(endpoints);
    }

    /// <summary>
    /// Returns the application part manager MVC uses. If MVC is not registered yet, registers one with the application's
    /// default parts, exactly as <c>AddControllers()</c> would create it. <c>AddControllers()</c> then uses this one.
    /// </summary>
    private static ApplicationPartManager GetOrAddApplicationPartManager(IHostApplicationBuilder builder)
    {
        if (builder.Services.LastOrDefault(service => service.ServiceType == typeof(ApplicationPartManager))?.ImplementationInstance
            is ApplicationPartManager existing)
        {
            return existing;
        }

        var manager = new ApplicationPartManager();
        if (!string.IsNullOrEmpty(builder.Environment.ApplicationName))
        {
            AddDefaultParts(manager, Assembly.Load(new AssemblyName(builder.Environment.ApplicationName)));
        }

        builder.Services.AddSingleton(manager);
        return manager;
    }

    /// <summary>
    /// Adds the parts MVC adds by default: the entry assembly, the assemblies it names with
    /// <see cref="ApplicationPartAttribute"/>, and their related assemblies (such as compiled Razor views).
    /// </summary>
    private static void AddDefaultParts(ApplicationPartManager manager, Assembly entryAssembly)
    {
        var assemblies = WithRelated(entryAssembly).Concat(entryAssembly
            .GetCustomAttributes<ApplicationPartAttribute>()
            .Select(attribute => Assembly.Load(new AssemblyName(attribute.AssemblyName)))
            .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
            .SelectMany(WithRelated));

        foreach (var assembly in assemblies.Distinct())
        {
            AddParts(manager, assembly);
        }

        static IEnumerable<Assembly> WithRelated(Assembly assembly) =>
            RelatedAssemblyAttribute.GetRelatedAssemblies(assembly, throwOnError: false)
                .OrderBy(related => related.FullName, StringComparer.Ordinal)
                .Prepend(assembly);
    }

    private static void AddParts(ApplicationPartManager manager, Assembly assembly)
    {
        foreach (var part in ApplicationPartFactory.GetApplicationPartFactory(assembly).GetApplicationParts(assembly))
        {
            if (!manager.ApplicationParts.Any(existing => existing.GetType() == part.GetType() && existing.Name == part.Name))
            {
                manager.ApplicationParts.Add(part);
            }
        }
    }
}
