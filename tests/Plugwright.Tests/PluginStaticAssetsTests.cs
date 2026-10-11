using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Plugwright.AspNetCore;

namespace Plugwright.Tests;

/// <summary>
/// Guards of app.MapPluginStaticAssets(). Serving assets is tested in StaticAssetTests of Plugwright.IntegrationTests.
/// </summary>
public sealed class PluginStaticAssetsTests
{
    [Fact]
    public void Without_plugins_with_assets_no_style_sheets_are_linked()
    {
        var app = Build(applyPlugins: true);

        app.MapPluginStaticAssets();

        app.Services.GetRequiredService<PluginStaticAssets>().StyleSheets.ShouldBeEmpty();
    }

    [Fact]
    public void Mapping_twice_throws()
    {
        var app = Build(applyPlugins: true);
        app.MapPluginStaticAssets();

        Should.Throw<InvalidOperationException>(() => app.MapPluginStaticAssets()).Message.ShouldContain("already been called");
    }

    [Fact]
    public void Mapping_without_ApplyPlugins_throws()
    {
        var app = Build(applyPlugins: false);

        Should.Throw<InvalidOperationException>(() => app.MapPluginStaticAssets()).Message.ShouldContain("builder.ApplyPlugins()");
    }

    [Fact]
    public void Style_sheets_without_mapping_throw()
    {
        var app = Build(applyPlugins: true);

        Should.Throw<InvalidOperationException>(() => app.Services.GetRequiredService<PluginStaticAssets>().StyleSheets)
            .Message.ShouldContain("app.MapPluginStaticAssets()");
    }

    [Fact]
    public void Options_default_to_disk_without_compression()
    {
        var options = new PluginStaticAssetsOptions();

        options.Source.ShouldBe(PluginStaticAssetSource.Disk);
        options.CompressPackageAssets.ShouldBeFalse();
    }

    private static WebApplication Build(bool applyPlugins)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.PluginHandlers.Add<BlazorPluginHandler>();
        if (applyPlugins)
        {
            builder.ApplyPlugins();
        }
        else
        {
            builder.Services.AddSingleton<PluginStaticAssets>();
        }

        return builder.Build();
    }
}
