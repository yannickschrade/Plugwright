using Microsoft.Extensions.Hosting;
using Plugwright;

#region host
var builder = Host.CreateApplicationBuilder(args);

builder.Plugins.AddFromPackages(Path.Combine(AppContext.BaseDirectory, "packages"));

// Only load packages, plugins and libraries alike, signed by Acme. The sample certificate is self-signed,
// so it is its own issuer; with a certificate from a CA, IssuerCertificate is the CA's root certificate.
builder.PluginVerifiers.AddPackageSignatureVerifier(options =>
    options.TrustedPublishers.Add(new TrustedPublisher
    {
        IssuerCertificate = Path.Combine(AppContext.BaseDirectory, "acme-packages.crt"),
        Subject = "CN=Acme Packages, O=Acme",
    }));

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();

await app.RunAsync();
#endregion
