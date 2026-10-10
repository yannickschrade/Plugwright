namespace Plugwright.Tests;

public sealed class PluginVerifierCollectionTests
{
    [Fact]
    public void Verifiers_are_optional_and_empty_by_default()
    {
        TestHost.CreateBuilder().PluginVerifiers.ShouldBeEmpty();
    }

    [Fact]
    public void AddHashVerifier_resolves_the_lock_file_against_the_content_root()
    {
        var builder = TestHost.CreateBuilder();

        builder.PluginVerifiers.AddHashVerifier(options => options.LockFile = "locks/plugins.lock.json");

        LockFileMessage(builder).ShouldContain(Path.Combine(builder.Environment.ContentRootPath, "locks", "plugins.lock.json"));
    }

    [Fact]
    public void AddHashVerifier_defaults_to_plugins_lock_json()
    {
        var builder = TestHost.CreateBuilder();

        builder.PluginVerifiers.AddHashVerifier();

        LockFileMessage(builder).ShouldContain(Path.Combine(builder.Environment.ContentRootPath, "plugins.lock.json"));
    }

    [Fact]
    public void AddHashVerifier_binds_settings_from_a_configuration_section()
    {
        var builder = TestHost.CreateBuilder(new Dictionary<string, string?>
        {
            ["Plugwright:Verification:Hash:LockFile"] = "/etc/myapp/plugins.lock.json",
        });

        builder.PluginVerifiers.AddHashVerifier(builder.Configuration.GetSection("Plugwright:Verification:Hash"));

        LockFileMessage(builder).ShouldContain("/etc/myapp/plugins.lock.json");
    }

    [Fact]
    public void Adding_the_same_verifier_type_twice_throws()
    {
        var builder = TestHost.CreateBuilder();
        builder.PluginVerifiers.AddHashVerifier();

        Should.Throw<ArgumentException>(() => builder.PluginVerifiers.AddHashVerifier());
    }

    [Fact]
    public void Adding_a_verifier_after_ApplyPlugins_throws()
    {
        var builder = TestHost.CreateBuilder();
        builder.ApplyPlugins();

        Should.Throw<InvalidOperationException>(() => builder.PluginVerifiers.AddHashVerifier());
    }

    [Fact]
    public void Plugins_added_in_code_are_not_verified()
    {
        var builder = TestHost.CreateBuilder();
        builder.PluginVerifiers.AddHashVerifier(options => options.LockFile = "does-not-exist.json");
        builder.Plugins.Add<Plugins.MarkerPlugin>();

        Should.NotThrow(builder.ApplyPlugins);
    }

    // The lock file path shows up in the message of a lock file that does not exist.
    private static string LockFileMessage(Microsoft.Extensions.Hosting.IHostApplicationBuilder builder) =>
        Should.Throw<InvalidOperationException>(() => builder.PluginVerifiers.Single()
            .Verify(new PluginVerificationRequest(new PluginDirectorySource("plugins"), "Acme", "/plugins/Acme", []))).Message;
}
