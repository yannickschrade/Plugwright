using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Plugwright;

/// <summary>
/// The signed manifest of a plugin folder, verified by the <see cref="CertificateVerifier"/>.
/// </summary>
/// <remarks>
/// The manifest is a JSON document with the plugin ID, the plugin version and the hash of every other file in the
/// folder. It is stored as a CMS signature with the JSON as embedded content, in <see cref="FileName"/>.
/// </remarks>
internal sealed class PluginManifest
{
    public const string FileName = "plugwright.manifest.p7s";
    public const int CurrentVersion = 1;

    private PluginManifest(string pluginId, string pluginVersion, Dictionary<string, string> files)
    {
        PluginId = pluginId;
        PluginVersion = pluginVersion;
        Files = files;
    }

    public string PluginId { get; }

    public string PluginVersion { get; }

    /// <summary>
    /// Gets the hash of every file in the plugin folder except the manifest, by relative path.
    /// </summary>
    public IReadOnlyDictionary<string, string> Files { get; }

    /// <summary>
    /// Creates the manifest for a plugin folder. ID and version are read from the folder's main assembly.
    /// </summary>
    public static PluginManifest Create(string folder)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
        var mainAssemblyPath = Path.Combine(folder, $"{name}.dll");
        if (!File.Exists(mainAssemblyPath))
        {
            throw new InvalidOperationException(
                $"The folder '{folder}' contains no '{name}.dll'. Each plugin folder must contain an assembly with the folder's name.");
        }

        var metadata = PluginMetadataReader.ReadWithoutLoading(mainAssemblyPath, verifiedContent: null).Metadata;
        var (_, files) = VerifiedFiles.Read(folder);
        var hashes = files
            .Where(file => file.Path != FileName)
            .ToDictionary(file => file.Path, file => PluginFileHash.Compute(file.Content.Span), StringComparer.Ordinal);

        return new PluginManifest(metadata.Id, metadata.Version, hashes);
    }

    /// <summary>
    /// Signs the manifest with a certificate that has a private key.
    /// </summary>
    /// <param name="certificate">The signing certificate, with private key.</param>
    /// <param name="intermediateCertificates">Certificates between the signing certificate and the root, embedded so that verifiers can build the chain.</param>
    /// <returns>The content of the manifest file.</returns>
    public byte[] Sign(X509Certificate2 certificate, IEnumerable<X509Certificate2> intermediateCertificates)
    {
        var signer = new CmsSigner(certificate) { IncludeOption = X509IncludeOption.EndCertOnly };
        signer.Certificates.AddRange(intermediateCertificates.ToArray());

        var signedCms = new SignedCms(new ContentInfo(ToJson()), detached: false);
        signedCms.ComputeSignature(signer);
        return signedCms.Encode();
    }

    /// <summary>
    /// Decodes a manifest file and checks that the signature matches the content. Does not decide whether the signer is trusted.
    /// </summary>
    /// <exception cref="CryptographicException">The file is not a valid signature, or the signature does not match.</exception>
    /// <exception cref="InvalidDataException">The signed content is not a valid manifest.</exception>
    public static (PluginManifest Manifest, SignerInfo Signer, X509Certificate2Collection Certificates) Decode(ReadOnlySpan<byte> manifestFile)
    {
        var signedCms = new SignedCms();
        signedCms.Decode(manifestFile);
        signedCms.CheckSignature(verifySignatureOnly: true);

        if (signedCms.SignerInfos.Count != 1)
        {
            throw new InvalidDataException($"The manifest must have exactly one signer, but has {signedCms.SignerInfos.Count}.");
        }

        return (Parse(signedCms.ContentInfo.Content), signedCms.SignerInfos[0], signedCms.Certificates);
    }

    private byte[] ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", CurrentVersion);
            writer.WriteString("pluginId", PluginId);
            writer.WriteString("pluginVersion", PluginVersion);
            writer.WriteStartObject("files");
            foreach (var (path, hash) in Files.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteString(path, hash);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static PluginManifest Parse(byte[] json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("version").GetInt32() != CurrentVersion)
            {
                throw new InvalidDataException($"The manifest has an unsupported version. Expected version {CurrentVersion}.");
            }

            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in root.GetProperty("files").EnumerateObject())
            {
                files.Add(file.Name, file.Value.GetString()!);
            }

            return new PluginManifest(root.GetProperty("pluginId").GetString()!, root.GetProperty("pluginVersion").GetString()!, files);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidDataException($"The manifest content is invalid: {exception.Message}", exception);
        }
    }
}
