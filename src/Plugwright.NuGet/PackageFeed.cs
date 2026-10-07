using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Plugwright;

/// <summary>
/// Downloads library packages from a NuGet feed.
/// </summary>
internal sealed class PackageFeed
{
    private readonly string _feed;

    /// <summary>
    /// Gets the feed as configured, for messages.
    /// </summary>
    public string Name => _feed;
    private readonly TimeSpan _timeout;
    private readonly SourceRepository _repository;

    /// <param name="feed">A URL, a local folder, or the name of a package source in NuGet.Config.</param>
    /// <param name="contentRoot">Where NuGet.Config is searched from, and relative folders are resolved against.</param>
    /// <param name="timeout">How long one download may take.</param>
    public PackageFeed(string feed, string contentRoot, TimeSpan timeout)
    {
        _feed = feed;
        _timeout = timeout;

        // A configured source brings its credentials along.
        var settings = Settings.LoadDefaultSettings(contentRoot);
        var source = new PackageSourceProvider(settings).LoadPackageSources()
            .FirstOrDefault(source => source.Name == feed || source.Source == feed)
            ?? new PackageSource(Uri.TryCreate(feed, UriKind.Absolute, out _) ? feed : Path.GetFullPath(feed, contentRoot));
        _repository = Repository.Factory.GetCoreV3(source);
    }

    /// <summary>
    /// Downloads the lowest version of a package that matches the range.
    /// </summary>
    /// <returns>The version and the content of the .nupkg file.</returns>
    /// <exception cref="InvalidOperationException">The feed is unreachable, has no matching version, or the download timed out.</exception>
    public (NuGetVersion Version, byte[] Content) Download(string packageId, VersionRange range)
    {
        using var cancellation = new CancellationTokenSource(_timeout);
        using var cache = new SourceCacheContext { NoCache = true, DirectDownload = true };
        try
        {
            var resource = _repository.GetResourceAsync<FindPackageByIdResource>(cancellation.Token).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException($"'{_feed}' is not a NuGet feed that packages can be downloaded from.");
            var versions = resource.GetAllVersionsAsync(packageId, cache, NullLogger.Instance, cancellation.Token).GetAwaiter().GetResult();
            var version = (versions ?? []).Where(range.Satisfies).Order().FirstOrDefault()
                ?? throw new InvalidOperationException(
                    $"The feed '{_feed}' has no version of package '{packageId}' that matches {range.ToNormalizedString()}.");

            using var stream = new MemoryStream();
            if (!resource.CopyNupkgToStreamAsync(packageId, version, stream, cache, NullLogger.Instance, cancellation.Token).GetAwaiter().GetResult())
            {
                throw new InvalidOperationException($"The feed '{_feed}' could not provide package '{packageId}' {version.ToNormalizedString()}.");
            }

            return (version, stream.ToArray());
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"Downloading package '{packageId}' from the feed '{_feed}' did not finish within {_timeout.TotalSeconds:0} seconds.");
        }
        catch (Exception exception) when (exception is FatalProtocolException or HttpRequestException or IOException)
        {
            throw new InvalidOperationException(
                $"Package '{packageId}' could not be downloaded from the feed '{_feed}': {exception.Message}", exception);
        }
    }
}
