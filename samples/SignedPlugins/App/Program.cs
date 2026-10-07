using Microsoft.Extensions.Hosting;
using Plugwright;

#region host
var builder = Host.CreateApplicationBuilder(args);

builder.Plugins.AddFromDirectory(Path.Combine(AppContext.BaseDirectory, "plugins"));

// Only load plugins signed by Acme: certificates for "CN=Acme Plugins, O=Acme", issued by Acme's CA.
builder.PluginVerifiers.AddCertificateVerifier(options =>
    options.TrustedPublishers.Add(new TrustedPublisher
    {
        IssuerCertificate = Path.Combine(AppContext.BaseDirectory, "acme-ca.crt"),
        Subject = "CN=Acme Plugins, O=Acme",
    }));

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();

await app.RunAsync();
#endregion
