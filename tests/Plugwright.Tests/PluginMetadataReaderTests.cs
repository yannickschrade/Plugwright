using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class PluginMetadataReaderTests
{
    [Fact]
    public void Reads_id_version_and_attribute_metadata_from_the_assembly()
    {
        var info = PluginMetadataReader.Read(typeof(MarkerPlugin).Assembly);

        info.Metadata.Id.ShouldBe("Plugwright.Tests");
        info.Metadata.Version.ShouldBe("0.1.0");
        info.AssemblyVersion.ShouldBe(new Version(0, 1, 0, 0));
        info.References.ShouldContain(reference => reference.Name == "Plugwright.Abstractions");
    }

    [Fact]
    public void Attributes_fill_gaps_but_never_override_project_metadata()
    {
        // The test project sets no <Description>, but inherits <Authors> from Directory.Build.props.
        // AssemblyInfo.cs declares both with [Plugin].
        var metadata = PluginMetadataReader.Read(typeof(MarkerPlugin).Assembly).Metadata;

        metadata.Description.ShouldBe("Test plugins for the unit tests.");
        metadata.Authors.ShouldBe("Yannick Schrade");
    }

    [Fact]
    public void Project_metadata_wins_over_attributes()
    {
        // Plugwright.Abstractions sets <Description> and <Authors> in its project and has no [Plugin] attribute.
        var metadata = PluginMetadataReader.Read(typeof(IPlugin).Assembly).Metadata;

        metadata.Description.ShouldStartWith("Plugin contract");
        metadata.Authors.ShouldBe("Yannick Schrade");
        metadata.Dependencies.ShouldBeEmpty();
    }
}
