// Turns the Markdown that `docfx metadata` writes (outputFormat "markdown") into Markdown that Zensical renders:
//
// - <xref href="Uid"> tags become links to the API page and anchor of that uid, or to Microsoft Learn for .NET types.
//   An xref to a Plugwright uid without page fails, so that broken references in XML comments fail the docs build.
// - <pre><code class="lang-x"> blocks become fenced code blocks, so that they get syntax highlighting.
// - toc.yml is replaced by SUMMARY.md: the navigation of the API folder (Zensical's literate-nav). As on Microsoft
//   Learn, every namespace is a node with its overview page and its types, grouped by kind.
//
// Usage: dotnet run docs/tools/FixApiMarkdown.cs -- <api folder>

using System.Net;
using System.Text.RegularExpressions;

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: dotnet run docs/tools/FixApiMarkdown.cs -- <api folder>");
    return 1;
}

var apiFolder = args[0];
// SUMMARY.md is this tool's own output from an earlier run, not a page of DocFX.
var pages = Directory.GetFiles(apiFolder, "*.md")
    .Where(file => Path.GetFileName(file) != "SUMMARY.md")
    .ToDictionary(file => Path.GetRelativePath(apiFolder, file), File.ReadAllText);

// Every anchor DocFX writes is a uid with all other characters replaced by '_', for example
// Plugwright_PluginContext_Builder. Remember the page of each anchor and the display name of each page.
var pageOfAnchor = new Dictionary<string, string>(StringComparer.Ordinal);
var titleOfPage = new Dictionary<string, string>(StringComparer.Ordinal);
var kindOfPage = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (var (page, content) in pages)
{
    foreach (Match anchor in AnchorPattern().Matches(content))
    {
        pageOfAnchor.TryAdd(anchor.Groups["id"].Value, page);
    }

    var title = TitlePattern().Match(content);
    titleOfPage[page] = title.Success ? title.Groups["name"].Value.Replace("\\>", ">", StringComparison.Ordinal) : Path.GetFileNameWithoutExtension(page);
    kindOfPage[page] = title.Success ? title.Groups["kind"].Value : "Class";
}

var unresolved = new List<string>();
foreach (var (page, content) in pages)
{
    var fixedContent = XrefPattern().Replace(content, match => Link(Uri.UnescapeDataString(match.Groups["uid"].Value), page));
    fixedContent = CodeBlockPattern().Replace(fixedContent, match =>
        $"\n```{match.Groups["language"].Value}\n{WebUtility.HtmlDecode(match.Groups["code"].Value)}\n```\n");
    File.WriteAllText(Path.Combine(apiFolder, page), fixedContent);
}

File.Delete(Path.Combine(apiFolder, "toc.yml"));
File.WriteAllText(Path.Combine(apiFolder, "SUMMARY.md"), Summary());

if (unresolved.Count > 0)
{
    foreach (var message in unresolved.Distinct())
    {
        Console.Error.WriteLine(message);
    }

    return 1;
}

Console.WriteLine($"Fixed {pages.Count} API pages in {apiFolder}.");
return 0;

string Link(string uid, string page)
{
    var isOverloadGroup = uid.EndsWith('*');
    var id = AnchorId(uid.TrimEnd('*'));
    var anchor = isOverloadGroup
        ? pageOfAnchor.Keys.Where(candidate => candidate == id || candidate.StartsWith(id + "_", StringComparison.Ordinal)).Order().FirstOrDefault()
        : pageOfAnchor.ContainsKey(id) ? id : null;

    // Members without an anchor of their own, such as enum fields, link to the page of their type.
    var parent = uid.Split('(')[0];
    while (anchor is null && parent.LastIndexOf('.') is var dot and > 0)
    {
        parent = parent[..dot];
        if (pageOfAnchor.TryGetValue(AnchorId(parent), out var parentPage) && TitleId(parentPage) == AnchorId(parent))
        {
            return $"[`{titleOfPage[parentPage]}.{MemberName(uid)}`]({parentPage})";
        }
    }

    if (anchor is null)
    {
        if (uid.StartsWith("Plugwright", StringComparison.Ordinal))
        {
            unresolved.Add($"{page}: no API page for '{uid}'.");
            return $"`{uid}`";
        }

        return $"[`{ShortName(uid)}`](https://learn.microsoft.com/dotnet/api/{LearnPath(uid)})";
    }

    var targetPage = pageOfAnchor[anchor];
    var typeId = TitleId(targetPage);
    var title = titleOfPage[targetPage];
    var text = anchor == typeId ? title : $"{title}.{MemberName(uid)}";
    var target = anchor == typeId ? targetPage : $"{targetPage}#{anchor}";
    return $"[`{text}`]({target})";
}

// The anchor in the title of a page, which is the anchor of its type.
string TitleId(string page) => AnchorPattern().Match(pages[page]).Groups["id"].Value;

// The navigation of the API folder: a node per namespace with its overview page and its types grouped by kind.
string Summary()
{
    string[] kinds = ["Class", "Struct", "Interface", "Enum", "Delegate"];
    var lines = new List<string>();
    var namespacePages = kindOfPage
        .Where(entry => entry.Value == "Namespace")
        .Select(entry => entry.Key)
        .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.Ordinal);
    foreach (var namespacePage in namespacePages)
    {
        var namespaceName = Path.GetFileNameWithoutExtension(namespacePage);
        lines.Add($"- {namespaceName}");
        lines.Add($"    - [Overview]({namespacePage})");
        foreach (var kind in kinds)
        {
            var types = kindOfPage
                .Where(entry => entry.Value == kind && NamespaceOf(entry.Key) == namespaceName)
                .Select(entry => entry.Key)
                .OrderBy(type => titleOfPage[type], StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (types.Count == 0)
            {
                continue;
            }

            lines.Add($"    - {(kind == "Class" ? "Classes" : kind + "s")}");
            lines.AddRange(types.Select(type => $"        - [{EscapeLinkText(titleOfPage[type])}]({type})"));
        }
    }

    return string.Join('\n', lines) + "\n";
}

// The namespace of a type page: its name without the type name, Plugwright.AspNetCore.WebPluginHandler.md -> Plugwright.AspNetCore.
static string NamespaceOf(string page)
{
    var name = Path.GetFileNameWithoutExtension(page);
    return name[..name.LastIndexOf('.')];
}

// Escapes the angle brackets of generic types, and lets long names wrap before inner capitals
// (PluginHost|Application|Builder|Extensions) with a zero-width space instead of in the middle of a word.
static string EscapeLinkText(string text) =>
    InnerCapitalPattern().Replace(text, "&#8203;").Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

static string AnchorId(string uid) => NonWordPattern().Replace(uid, "_");

// The name of a member without parameters and generic arity: Plugwright.IPlugin.Configure(Plugwright.PluginContext) -> Configure.
static string MemberName(string uid)
{
    var withoutParameters = uid.Split('(')[0].TrimEnd('*');
    return withoutParameters[(withoutParameters.LastIndexOf('.') + 1)..].Split('`')[0];
}

// The last part of a uid, as DocFX shows references: System.AppContext.BaseDirectory -> BaseDirectory.
static string ShortName(string uid) => MemberName(uid);

static string LearnPath(string uid) =>
    uid.Split('(')[0].TrimEnd('*').Replace('`', '-').ToLowerInvariant();

internal partial class Program
{
    [GeneratedRegex("<a id=\"(?<id>[^\"]+)\"></a>")]
    private static partial Regex AnchorPattern();

    [GeneratedRegex(@"^# <a id=""[^""]+""></a> (?<kind>\w+) (?<name>.+)$", RegexOptions.Multiline)]
    private static partial Regex TitlePattern();

    [GeneratedRegex(@"<xref href=""(?<uid>[^""]+)""[^>]*></xref>")]
    private static partial Regex XrefPattern();

    [GeneratedRegex(@"<pre><code class=""lang-(?<language>[\w-]+)"">(?<code>.*?)</code></pre>", RegexOptions.Singleline)]
    private static partial Regex CodeBlockPattern();

    [GeneratedRegex(@"\W")]
    private static partial Regex NonWordPattern();

    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z])")]
    private static partial Regex InnerCapitalPattern();
}
