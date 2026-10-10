using System.Reflection;

namespace Plugwright.Tests;

public sealed class PluginDependencyResolverTests
{
    private readonly List<(string PluginId, string Reason)> _failures = [];

    [Fact]
    public void Orders_dependencies_first_and_keeps_the_original_order_otherwise()
    {
        var reports = Unit("Reports", dependencies: [new PluginDependency("Logging")]);
        var audit = Unit("Audit");
        var logging = Unit("Logging");

        var sorted = Resolve(reports, audit, logging);

        sorted.Select(unit => unit.Id).ShouldBe(["Logging", "Reports", "Audit"]);
        _failures.ShouldBeEmpty();
        reports.DependsOn.ShouldBe([logging]);
        reports.Metadata.Dependencies.Select(dependency => dependency.ToString()).ShouldBe(["Logging"]);
    }

    [Fact]
    public void Assembly_reference_to_another_plugin_is_a_dependency()
    {
        var reports = Unit("Reports", references: [new AssemblyName("Logging, Version=1.0.0.0"), new AssemblyName("System.Runtime")]);
        var logging = Unit("Logging", assemblyVersion: new Version(1, 0, 0, 0));

        Resolve(reports, logging).Select(unit => unit.Id).ShouldBe(["Logging", "Reports"]);

        reports.Metadata.Dependencies.ShouldHaveSingleItem().PluginId.ShouldBe("Logging");
    }

    [Fact]
    public void Attribute_and_reference_to_the_same_plugin_are_both_checked()
    {
        var reports = Unit(
            "Reports",
            dependencies: [new PluginDependency("Logging", "1.2.0")],
            references: [new AssemblyName("Logging, Version=2.0.0.0")]);
        var logging = Unit("Logging", version: "1.5.0", assemblyVersion: new Version(1, 5, 0, 0));

        Resolve(reports, logging);

        _failures.ShouldHaveSingleItem().Reason.ShouldContain("compiled against assembly version 2.0.0.0");
    }

    [Fact]
    public void Missing_dependency_is_reported()
    {
        var reports = Unit("Reports", dependencies: [new PluginDependency("Logging")]);

        Resolve(reports);

        _failures.ShouldBe([("Reports", "It depends on plugin 'Logging', which is not available.")]);
    }

    [Theory]
    [InlineData("1.5.0", "1.4.0", true)]
    [InlineData("1.5.0", "1.5.0", true)]
    [InlineData("1.5.0", "1.5.0-beta", true)]
    [InlineData("1.5.0", "1.6.0", false)]
    [InlineData("1.5.0-beta", "1.5.0", false)]
    public void Minimum_version_is_checked(string availableVersion, string minVersion, bool satisfied)
    {
        var reports = Unit("Reports", dependencies: [new PluginDependency("Logging", minVersion)]);
        var logging = Unit("Logging", version: availableVersion);

        Resolve(reports, logging);

        if (satisfied)
        {
            _failures.ShouldBeEmpty();
        }
        else
        {
            _failures.ShouldBe([("Reports",
                $"It requires plugin 'Logging' version {minVersion} or later, but version {availableVersion} is available.")]);
        }
    }

    [Theory]
    [InlineData("1.5.0", true)]
    [InlineData("2.0.0", false)]
    public void Version_range_is_checked_by_the_source(string availableVersion, bool satisfied)
    {
        var dependency = new PluginDependency("Logging", "1.2.0") { VersionRange = "[1.2.0, 2.0.0)" };
        var reports = Unit("Reports", dependencies: [dependency], satisfiesVersionRange: (_, version) => version.StartsWith('1'));

        Resolve(reports, Unit("Logging", version: availableVersion));

        if (satisfied)
        {
            _failures.ShouldBeEmpty();
            reports.Metadata.Dependencies.ShouldHaveSingleItem().ToString().ShouldBe("Logging [1.2.0, 2.0.0)");
        }
        else
        {
            _failures.ShouldBe([("Reports", $"It requires plugin 'Logging' version [1.2.0, 2.0.0), but version {availableVersion} is available.")]);
        }
    }

    [Fact]
    public void Invalid_minimum_version_is_reported()
    {
        var reports = Unit("Reports", dependencies: [new PluginDependency("Logging", "latest")]);

        Resolve(reports, Unit("Logging"));

        _failures.ShouldHaveSingleItem().Reason.ShouldContain("invalid minimum version 'latest'");
    }

    [Fact]
    public void Plugin_ids_are_compared_case_insensitively()
    {
        var reports = Unit("Reports", dependencies: [new PluginDependency("logging")]);
        var logging = Unit("Logging");

        Resolve(reports, logging).Select(unit => unit.Id).ShouldBe(["Logging", "Reports"]);
        _failures.ShouldBeEmpty();
    }

    [Fact]
    public void Every_plugin_in_a_cycle_is_reported()
    {
        var a = Unit("A", dependencies: [new PluginDependency("B")]);
        var b = Unit("B", dependencies: [new PluginDependency("C")]);
        var c = Unit("C", dependencies: [new PluginDependency("A")]);
        var d = Unit("D");

        var sorted = Resolve(a, b, c, d);

        _failures.Select(failure => failure.PluginId).Order().ShouldBe(["A", "B", "C"]);
        _failures.ShouldAllBe(failure => failure.Reason == "Plugins depend on each other in a cycle: A -> B -> C -> A.");
        sorted.ShouldContain(d);
    }

    [Fact]
    public void Plugin_added_in_code_cannot_depend_on_a_plugin_from_a_directory()
    {
        var inCode = PluginUnit.FromCode(typeof(PluginDependencyResolverTests).Assembly);
        inCode.PluginTypes.Add(typeof(Plugins.MarkerPlugin));
        var other = Unit("Plugwright.Abstractions");

        Resolve(inCode, other);

        _failures.ShouldHaveSingleItem().Reason.ShouldContain("Plugins added in code can only depend on plugins added in code.");
    }

    private List<PluginUnit> Resolve(params List<PluginUnit> units) =>
        PluginDependencyResolver.Resolve(units, (unit, state, reason) =>
        {
            unit.State = state;
            _failures.Add((unit.Id, reason));
        });

    private static PluginUnit Unit(
        string id,
        string version = "1.0.0",
        Version? assemblyVersion = null,
        List<PluginDependency>? dependencies = null,
        List<AssemblyName>? references = null,
        Func<PluginDependency, string, bool>? satisfiesVersionRange = null)
    {
        var metadata = new PluginMetadata(id, version) { Dependencies = dependencies ?? [] };
        var info = new PluginAssemblyInfo(metadata, assemblyVersion ?? new Version(1, 0, 0, 0), references ?? [])
        {
            SatisfiesVersionRange = satisfiesVersionRange,
        };
        return PluginUnit.FromFolder(info, $"/plugins/{id}", new NoAssemblies(id));
    }

    /// <summary>
    /// The resolver never loads anything, so the units need no real assemblies.
    /// </summary>
    private sealed class NoAssemblies(string name) : PluginAssemblies
    {
        public override string MainAssemblyName => name;

        public override Assembly LoadMainAssembly(System.Runtime.Loader.AssemblyLoadContext context) => throw new NotSupportedException();

        public override Assembly? LoadPrivateAssembly(System.Runtime.Loader.AssemblyLoadContext context, AssemblyName assemblyName) => null;

        public override string? ResolveUnmanagedDll(string unmanagedDllName) => null;
    }
}
