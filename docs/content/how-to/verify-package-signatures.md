# Verify package signatures

The [`PackageSignatureVerifier`](../api/Plugwright.PackageSignatureVerifier.md) only loads NuGet packages that are signed by a certificate you trust and unchanged since signing. It checks every package in a package folder: plugin packages and the library packages they use.

The complete, runnable project is in `samples/SignedPackages` in the repository.

## Sign packages

Publishers sign packages with the .NET SDK:

```bash
dotnet nuget sign Acme.Weather.1.0.0.nupkg \
  --certificate-path acme-packages.pfx \
  --timestamper http://timestamp.digicert.com
```

`dotnet nuget sign` needs an RSA code signing certificate that chains to a root the signing machine trusts, usually from a public certificate authority. Packages from nuget.org already carry a repository signature by nuget.org.

!!! note

    The sample creates a self-signed certificate during the build and signs with NuGet's signing API, because its sample certificate authority is not trusted by your machine.

## Register the verifier

Add the verifier, from the `Plugwright.NuGet` package, before calling `ApplyPlugins()`:

```csharp
--8<-- "samples/SignedPackages/App/Program.cs:host"
```

Trust works exactly as for [signed plugin folders](sign-plugins.md#trust-the-publisher): by certificate thumbprint, or as publisher with the certificate authority's root certificate and the signer's subject. Revocation checking is configured the same way. To take the settings from configuration, pass a configuration section instead of the delegate.

## What is checked

A package passes when:

- it is signed,
- its signature is valid and its content matches the signature, so nothing was added or changed after signing, and
- the signing certificate is trusted and valid now.

The primary signature counts: the author signature for packages the author signed, the repository signature for packages that only a repository such as nuget.org signed. To trust nuget.org's repository signature, trust its certificate.

!!! warning "Important"

    Timestamps in package signatures are not used. The signing certificate must be valid when the host verifies the package. Packages signed with a certificate that has expired since fail, even if the signature carries a timestamp. Use the [lock file](load-plugins-from-packages.md#verify-packages) for packages whose signing certificate may have expired, such as older packages from nuget.org.

## See also

- [Load plugins from NuGet packages](load-plugins-from-packages.md)
- [Security model](../explanation/security-model.md)
