using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Plugwright.Cli;

/// <summary>
/// <c>plugwright sign &lt;plugin-folder&gt; --certificate &lt;file&gt; [--password &lt;password&gt;]</c>:
/// writes the signed manifest for the CertificateVerifier.
/// </summary>
internal static class SignCommand
{
    public const string PasswordVariable = "PLUGWRIGHT_CERTIFICATE_PASSWORD";

    private const string Usage = $"""
        Usage: plugwright sign <plugin-folder> --certificate <file> [--password <password>]

        Signs a plugin folder: writes {PluginManifest.FileName} with the plugin ID, the plugin version and the sha256 hash
        of every file in the folder, signed with the certificate. The CertificateVerifier loads only plugins whose
        manifest is signed by a trusted certificate and whose files match the manifest exactly.

        Sign the folder after it is complete, for example after 'dotnet publish'. Signing again replaces the manifest.

        Arguments:
          <plugin-folder>             The plugin folder, containing <folder-name>.dll.

        Options:
          -c, --certificate <file>    A PKCS#12 file (.pfx, .p12) with the signing certificate and its private key.
                                      Intermediate certificates in the file are embedded in the signature.
          -p, --password <password>   The password of the certificate file. Default: the environment variable
                                      {PasswordVariable}.
          -h, --help                  Show help.
        """;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        string? folder = null;
        string? certificateFile = null;
        var password = Environment.GetEnvironmentVariable(PasswordVariable);

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    output.WriteLine(Usage);
                    return CliApp.Success;

                case "-c" or "--certificate" when i + 1 < args.Length:
                    certificateFile = args[++i];
                    break;

                case "-p" or "--password" when i + 1 < args.Length:
                    password = args[++i];
                    break;

                case "-c" or "--certificate" or "-p" or "--password":
                    return CliApp.Fail(error, $"Option '{args[i]}' needs a value.\n\n{Usage}");

                case var option when option.StartsWith('-'):
                    return CliApp.Fail(error, $"Unknown option '{option}'.\n\n{Usage}");

                case var argument when folder is null:
                    folder = argument;
                    break;

                default:
                    return CliApp.Fail(error, $"Unexpected argument '{args[i]}'.\n\n{Usage}");
            }
        }

        if (folder is null || certificateFile is null)
        {
            return CliApp.Fail(error, $"The plugin folder and the certificate are required.\n\n{Usage}");
        }

        if (!Directory.Exists(folder))
        {
            error.WriteLine($"The plugin folder '{folder}' does not exist.");
            return CliApp.Failure;
        }

        X509Certificate2Collection certificates;
        try
        {
            certificates = X509CertificateLoader.LoadPkcs12CollectionFromFile(certificateFile, password);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            error.WriteLine($"The certificate file '{certificateFile}' cannot be loaded: {exception.Message}");
            return CliApp.Failure;
        }

        var signingCertificate = certificates.FirstOrDefault(certificate => certificate.HasPrivateKey);
        if (signingCertificate is null)
        {
            error.WriteLine($"The certificate file '{certificateFile}' contains no certificate with a private key.");
            return CliApp.Failure;
        }

        PluginManifest manifest;
        try
        {
            manifest = PluginManifest.Create(folder);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or BadImageFormatException)
        {
            error.WriteLine(exception.Message);
            return CliApp.Failure;
        }

        // Embed intermediate certificates, but not self-signed roots: the verifier must have the root itself.
        var intermediates = certificates.Where(certificate =>
            certificate != signingCertificate && certificate.SubjectName.Name != certificate.IssuerName.Name);
        File.WriteAllBytes(Path.Combine(folder, PluginManifest.FileName), manifest.Sign(signingCertificate, intermediates));

        output.WriteLine(
            $"Signed {manifest.PluginId} {manifest.PluginVersion} ({manifest.Files.Count} files) with '{signingCertificate.Subject}', "
            + $"thumbprint {signingCertificate.GetCertHashString(HashAlgorithmName.SHA256)}.");
        return CliApp.Success;
    }
}
