using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Packaging;
using NuGet.Versioning;
using Plugwright.NuGet;

namespace Plugwright.Tests;

public sealed class PluginPackageTests
{
    private static readonly string s_runtime = RuntimeInformation.RuntimeIdentifier;

    [Fact]
    public void Reads_identity_type_and_metadata()
    {
        var package = Read(Build(isPlugin: true));

        package.Id.ShouldBe("Acme.Native");
        package.Version.ToNormalizedString().ShouldBe("1.2.0");
        package.IsPlugin.ShouldBeTrue();
        package.Description.ShouldBe("A package for tests.");
        package.Authors.ShouldBe("Acme");
        package.Dependencies.ShouldHaveSingleItem().Id.ShouldBe("Acme.Library");
    }

    [Fact]
    public void Package_without_plugin_type_is_a_library()
    {
        Read(Build(isPlugin: false)).IsPlugin.ShouldBeFalse();
    }

    [Fact]
    public void Picks_the_nearest_framework_and_prefers_runtime_specific_assemblies()
    {
        var package = Read(Build(isPlugin: true));

        Encoding.UTF8.GetString(package.Assemblies["Acme.Native"]).ShouldBe("net8.0");
        Encoding.UTF8.GetString(package.Assemblies["Acme.Helper"]).ShouldBe($"runtime {s_runtime}");
        package.Assemblies.ShouldNotContainKey("Acme.Legacy");
    }

    [Fact]
    public void Native_libraries_for_this_runtime_are_written_to_a_temporary_folder()
    {
        var package = Read(Build(isPlugin: true));
        var assemblies = new PackageAssemblies("Acme.Native", package.Assemblies, package.NativeLibraries);

        var path = assemblies.ResolveUnmanagedDll("acme");

        path.ShouldNotBeNull();
        Path.GetFileName(path).ShouldBe("libacme.so");
        File.ReadAllText(path).ShouldBe("native code");
        path.ShouldStartWith(Path.Combine(Path.GetTempPath(), "plugwright-native"));
        Path.GetFileName(Path.GetDirectoryName(path)).ShouldBe("Acme.Native");
        assemblies.ResolveUnmanagedDll("other").ShouldBeNull();
    }

    [Fact]
    public void Changed_native_library_on_disk_is_not_loaded()
    {
        var package = Read(Build(isPlugin: true));
        var assemblies = new PackageAssemblies("Acme.Native.Changed", package.Assemblies, package.NativeLibraries);
        var path = assemblies.ResolveUnmanagedDll("acme")!;

        File.WriteAllText(path, "replaced");

        Should.Throw<FileLoadException>(() => assemblies.ResolveUnmanagedDll("acme"));
    }

    [Fact]
    public void Invalid_package_is_reported_with_its_file_name()
    {
        var exception = Should.Throw<InvalidDataException>(() => PluginPackage.Read("/packages/Broken.nupkg", Encoding.UTF8.GetBytes("broken")));

        exception.Message.ShouldStartWith("'Broken.nupkg' is not a valid NuGet package");
    }

    private static PluginPackage Read(byte[] content) => PluginPackage.Read("/packages/Acme.Native.1.2.0.nupkg", content);

    /// <summary>
    /// Builds a package in memory. File contents are text that names where they come from, so tests can see which one was picked.
    /// </summary>
    private static byte[] Build(bool isPlugin)
    {
        var builder = new PackageBuilder
        {
            Id = "Acme.Native",
            Version = NuGetVersion.Parse("1.2.0"),
            Description = "A package for tests.",
        };
        builder.Authors.Add("Acme");
        if (isPlugin)
        {
            builder.PackageTypes.Add(new PackageType(PluginPackage.PluginPackageType, new Version(0, 0)));
        }

        builder.DependencyGroups.Add(new PackageDependencyGroup(
            NuGetFramework.Parse("net8.0"),
            [new PackageDependency("Acme.Library", VersionRange.Parse("1.0.0"))]));

        Add(builder, "lib/net8.0/Acme.Native.dll", "net8.0");
        Add(builder, "lib/net8.0/Acme.Helper.dll", "portable");
        Add(builder, "lib/net472/Acme.Legacy.dll", "net472");
        Add(builder, $"runtimes/{s_runtime}/lib/net8.0/Acme.Helper.dll", $"runtime {s_runtime}");
        Add(builder, $"runtimes/{s_runtime}/native/libacme.so", "native code");
        Add(builder, "runtimes/other-rid/native/libother.so", "other");

        using var stream = new MemoryStream();
        builder.Save(stream);
        return stream.ToArray();
    }

    private static void Add(PackageBuilder builder, string path, string content) =>
        builder.Files.Add(new InMemoryPackageFile(path, Encoding.UTF8.GetBytes(content)));

    private sealed class InMemoryPackageFile(string path, byte[] content) : IPackageFile
    {
        public string Path => path;

        public string? EffectivePath => null;

        public NuGetFramework? NuGetFramework => null;

        public FrameworkName? TargetFramework => null;

        public DateTimeOffset LastWriteTime => DateTimeOffset.UnixEpoch;

        public Stream GetStream() => new MemoryStream(content, writable: false);
    }
}
