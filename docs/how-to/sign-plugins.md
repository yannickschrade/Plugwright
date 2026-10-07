# Sign plugins and trust their publisher

The <xref:Plugwright.CertificateVerifier> only loads plugins signed by a publisher you trust. Unlike a [lock file](verify-plugins-with-a-lock-file.md), nothing changes on the host when a plugin is updated: the publisher signs the new version, and the host keeps trusting the publisher.

The complete, runnable project is in `samples/SignedPlugins` in the repository. It creates a sample certificate authority during the build.

## How it works

The publisher signs each plugin folder with `plugwright sign`. This writes `plugwright.manifest.p7s` into the folder: a signature over the plugin ID, the plugin version and the hash of every other file in the folder.

When the host loads the plugin, the verifier checks that:

- the signature is valid and the signing certificate is trusted,
- the certificate is valid now,
- the manifest is for the plugin installed in this folder, and
- the folder contains exactly the files listed in the manifest, unchanged.

## Sign a plugin

The publisher needs a certificate with a private key, as a PKCS#12 file (`.pfx` or `.p12`). Publish the plugin, then sign the folder:

```bash
dotnet publish Acme.Reports -c Release -o out/Acme.Reports
plugwright sign out/Acme.Reports --certificate acme-plugins.pfx
```

The tool reads the password from the environment variable `PLUGWRIGHT_CERTIFICATE_PASSWORD`, or from `--password`. Prefer the environment variable, so that the password does not end up in the shell history. Intermediate certificates in the PFX file are embedded in the signature.

Sign the folder last, after all files are in place. Signing again replaces the manifest. Ship the folder as it is, including `plugwright.manifest.p7s`.

## Trust the publisher

Register the verifier with at least one trusted certificate. There are two ways to trust.

### Trust a certificate authority and a subject

Trust every certificate that a certificate authority issues to a given subject. This survives certificate renewal: a renewed certificate with the same subject from the same authority is trusted without changes on the host.

[!code-csharp[](../../samples/SignedPlugins/App/Program.cs?name=host)]

`IssuerCertificate` is the certificate of the root authority, as a PEM or DER file. The signing certificate must chain up to it; intermediate certificates come from the signature. `Subject` is compared as a distinguished name, so spacing and case do not matter.

### Trust a single certificate

Trust exactly one certificate by its thumbprint, as SHA-256 or SHA-1. `plugwright sign` prints the SHA-256 thumbprint of the certificate it used.

```csharp
builder.PluginVerifiers.AddCertificateVerifier(options =>
    options.TrustedThumbprints.Add("9F2DB14A363EBF6A1DA40F4229766BD35E55261904EBD500AEE48B0C8869EB60"));
```

This suits self-signed certificates. When the certificate is renewed, add the new thumbprint.

### Take the settings from configuration

Bind the settings to a configuration section explicitly:

```csharp
builder.PluginVerifiers.AddCertificateVerifier(builder.Configuration.GetSection("Plugwright:Verification:Certificate"));
```

```json
{
  "Plugwright": {
    "Verification": {
      "Certificate": {
        "TrustedPublishers": [
          { "IssuerCertificate": "/etc/myapp/acme-ca.crt", "Subject": "CN=Acme Plugins, O=Acme" }
        ],
        "RevocationMode": "Online"
      }
    }
  }
}
```

The trust settings and the issuer certificate are what you trust. Protect them, and `appsettings.json` if they come from there, from whoever can write to the plugin directory.

## Check for revocation

If a signing certificate is stolen, the authority revokes it. To reject revoked certificates, turn on revocation checking:

```csharp
options.RevocationMode = X509RevocationMode.Online;
```

| Mode | Behavior |
| --- | --- |
| `NoCheck` (default) | No revocation check. Machines without network access start normally. |
| `Online` | Checks the revocation lists or OCSP servers of the certificate. If the status cannot be determined, verification fails. |
| `Offline` | Uses only revocation lists cached on the machine. |

Revocation checking applies to trusted publishers. Certificates trusted by thumbprint are not checked; remove the thumbprint instead.

## When the certificate expires

Signatures carry no timestamp. The signing certificate must be valid when the host verifies the plugin. When it expires, sign the plugins again with the renewed certificate. With publisher trust, the host needs no change.

## Combine with a lock file

You can register both verifiers. A plugin then has to pass both: it must be signed by a trusted publisher and match the host's lock file. Create the lock file after signing, so that it covers `plugwright.manifest.p7s` as well.

## See also

- [Security model](../explanation/security-model.md)
- [CLI reference](../reference/cli.md#plugwright-sign)
- <xref:Plugwright.CertificateVerifierOptions>
