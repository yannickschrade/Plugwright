using System.Reflection;
using System.Runtime.Loader;

namespace Plugwright.Tests;

public sealed class SharedAssemblyTests
{
    [Theory]
    [InlineData("Plugwright")]
    [InlineData("Plugwright.Abstractions")]
    [InlineData("Plugwright.AspNetCore")]
    [InlineData("Plugwright.AspNetCore.Abstractions")]
    [InlineData("Microsoft.Extensions.Logging")]
    [InlineData("Microsoft.AspNetCore.Mvc.Core")]
    public void Plugin_contracts_and_Microsoft_Extensions_are_always_shared(string name)
    {
        new SharedAssemblies().Contains(new AssemblyName(name)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Plugwright.NuGet")]
    [InlineData("Acme.Contracts")]
    [InlineData("System.Text.Json")]
    public void Other_assemblies_are_not_shared_by_default(string name)
    {
        new SharedAssemblies().Contains(new AssemblyName(name)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore", true)]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer", true)]
    [InlineData("microsoft.entityframeworkcore.sqlite", true)]
    [InlineData("Microsoft.EntityFrameworkCoreX", false)]
    [InlineData("Microsoft.Entity", false)]
    public void Prefix_matches_the_name_itself_and_names_that_continue_after_a_dot(string name, bool shared)
    {
        var sharedAssemblies = new SharedAssemblies();
        sharedAssemblies.AddPrefix("Microsoft.EntityFrameworkCore");

        sharedAssemblies.Contains(new AssemblyName(name)).ShouldBe(shared);
    }

    [Fact]
    public void Name_matches_only_that_assembly()
    {
        var sharedAssemblies = new SharedAssemblies();
        sharedAssemblies.AddName("Acme.Contracts");

        sharedAssemblies.Contains(new AssemblyName("acme.contracts")).ShouldBeTrue();
        sharedAssemblies.Contains(new AssemblyName("Acme.Contracts.Ui")).ShouldBeFalse();
    }

    [Fact]
    public void ShareAssembly_shares_the_assembly_by_name()
    {
        var builder = TestHost.CreateBuilder();

        builder.Plugins.ShareAssembly(typeof(SharedAssemblyTests).Assembly).ShouldBeSameAs(builder.Plugins);

        builder.Plugins.SharedAssemblies.Contains(typeof(SharedAssemblyTests).Assembly.GetName()).ShouldBeTrue();
    }

    [Fact]
    public void ShareAssembly_with_an_assembly_outside_the_default_load_context_throws()
    {
        var builder = TestHost.CreateBuilder();
        var context = new AssemblyLoadContext("Other");
        var assembly = context.LoadFromAssemblyPath(typeof(SharedAssemblyTests).Assembly.Location);

        var exception = Should.Throw<ArgumentException>(() => builder.Plugins.ShareAssembly(assembly));

        exception.Message.ShouldContain("is not in the host's default load context");
    }

    [Fact]
    public void ShareAssemblies_shares_by_prefix()
    {
        var builder = TestHost.CreateBuilder();

        builder.Plugins.ShareAssemblies("Acme").ShouldBeSameAs(builder.Plugins);

        builder.Plugins.SharedAssemblies.Contains(new AssemblyName("Acme.Contracts")).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".Acme")]
    [InlineData("Acme.")]
    public void ShareAssemblies_with_an_invalid_prefix_throws(string prefix)
    {
        var builder = TestHost.CreateBuilder();

        Should.Throw<ArgumentException>(() => builder.Plugins.ShareAssemblies(prefix));
    }
}
