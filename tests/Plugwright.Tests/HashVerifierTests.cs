using System.Security.Cryptography;
using System.Text;

namespace Plugwright.Tests;

public sealed class HashVerifierTests : IDisposable
{
    private static readonly PluginDirectorySource s_source = new("plugins");

    private readonly string _directory = Directory.CreateTempSubdirectory("plugwright-hash-").FullName;

    private string LockFilePath => Path.Combine(_directory, "plugins.lock.json");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Plugin_matching_the_lock_file_passes()
    {
        WriteLockFile("Acme.Greeter", ("Acme.Greeter.dll", "main"), ("lib/Helper.dll", "helper"));

        var result = Verify(Request("Acme.Greeter", ("Acme.Greeter.dll", "main"), ("lib/Helper.dll", "helper")));

        result.Succeeded.ShouldBeTrue();
        result.Message.ShouldContain("all 2 files");
    }

    [Fact]
    public void Plugin_names_are_compared_case_insensitively()
    {
        WriteLockFile("Acme.Greeter", ("Acme.Greeter.dll", "main"));

        Verify(Request("acme.greeter", ("Acme.Greeter.dll", "main"))).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Plugin_without_entry_fails()
    {
        WriteLockFile("Acme.Other", ("Acme.Other.dll", "main"));

        var result = Verify(Request("Acme.Greeter", ("Acme.Greeter.dll", "main")));

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldBe($"The lock file '{LockFilePath}' has no entry for plugin 'Acme.Greeter'.");
    }

    [Fact]
    public void Changed_file_fails()
    {
        WriteLockFile("Acme.Greeter", ("Acme.Greeter.dll", "main"));

        var result = Verify(Request("Acme.Greeter", ("Acme.Greeter.dll", "changed")));

        result.Message.ShouldBe("File 'Acme.Greeter.dll' does not match its hash in the lock file.");
    }

    [Fact]
    public void Added_file_fails()
    {
        WriteLockFile("Acme.Greeter", ("Acme.Greeter.dll", "main"));

        var result = Verify(Request("Acme.Greeter", ("Acme.Greeter.dll", "main"), ("Evil.dll", "evil")));

        result.Message.ShouldBe("File 'Evil.dll' is not listed in the lock file.");
    }

    [Fact]
    public void Missing_file_fails()
    {
        WriteLockFile("Acme.Greeter", ("Acme.Greeter.dll", "main"), ("Acme.Greeter.deps.json", "deps"));

        var result = Verify(Request("Acme.Greeter", ("Acme.Greeter.dll", "main")));

        result.Message.ShouldBe("File 'Acme.Greeter.deps.json' is listed in the lock file but missing.");
    }

    [Fact]
    public void Unsupported_hash_algorithm_fails()
    {
        File.WriteAllText(LockFilePath, """
            { "version": 1, "plugins": { "Acme.Greeter": { "files": { "Acme.Greeter.dll": "md5-abc" } } } }
            """);

        var result = Verify(Request("Acme.Greeter", ("Acme.Greeter.dll", "main")));

        result.Message.ShouldContain("unsupported algorithm: 'md5'");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "plugins": {} }""")]
    [InlineData("""{ "version": 2, "plugins": {} }""")]
    [InlineData("""{ "version": 1 }""")]
    public void Invalid_lock_file_throws(string content)
    {
        File.WriteAllText(LockFilePath, content);

        var exception = Should.Throw<InvalidOperationException>(() => Verify(Request("Acme.Greeter", ("Acme.Greeter.dll", "main"))));

        exception.Message.ShouldContain(LockFilePath);
    }

    [Fact]
    public void Missing_lock_file_throws()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Verify(Request("Acme.Greeter", ("Acme.Greeter.dll", "main"))));

        exception.Message.ShouldBe($"The lock file '{LockFilePath}' does not exist.");
    }

    [Fact]
    public void Verifies_only_plugin_directories()
    {
        var verifier = new HashVerifier(LockFilePath);

        verifier.CanVerify(s_source).ShouldBeTrue();
        verifier.CanVerify(new PluginTypeSource(typeof(Plugins.MarkerPlugin))).ShouldBeFalse();
    }

    [Fact]
    public void Hashes_carry_the_algorithm_prefix()
    {
        var hash = PluginFileHash.Compute("main"u8);

        hash.ShouldBe("sha256-" + Convert.ToBase64String(SHA256.HashData("main"u8)));
    }

    private PluginVerificationResult Verify(PluginVerificationRequest request) => new HashVerifier(LockFilePath).Verify(request);

    private static PluginVerificationRequest Request(string name, params (string Path, string Content)[] files) =>
        new(s_source, name, $"/plugins/{name}", [.. files.Select(file => new PluginFile(file.Path, Encoding.UTF8.GetBytes(file.Content)))]);

    private void WriteLockFile(string name, params (string Path, string Content)[] files)
    {
        var entries = string.Join(", ", files.Select(file => $"\"{file.Path}\": \"{PluginFileHash.Compute(Encoding.UTF8.GetBytes(file.Content))}\""));
        File.WriteAllText(LockFilePath, $$"""{ "version": 1, "plugins": { "{{name}}": { "files": { {{entries}} } } } }""");
    }
}
