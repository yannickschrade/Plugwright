using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Plugwright.AspNetCore;

/// <summary>
/// Makes the files of another provider available below a path prefix, for example <c>_plugwright/Acme.Orders/</c>.
/// </summary>
internal sealed class PrefixFileProvider(string prefix, IFileProvider inner) : IFileProvider
{
    private readonly string _prefix = "/" + prefix.Trim('/') + "/";

    public IFileInfo GetFileInfo(string subpath) =>
        WithoutPrefix(subpath) is { } path ? inner.GetFileInfo(path) : new NotFoundFileInfo(subpath);

    public IDirectoryContents GetDirectoryContents(string subpath) =>
        WithoutPrefix(subpath) is { } path ? inner.GetDirectoryContents(path) : NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    private string? WithoutPrefix(string subpath)
    {
        var path = "/" + subpath.TrimStart('/');
        return path.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase) ? path[(_prefix.Length - 1)..] : null;
    }
}
