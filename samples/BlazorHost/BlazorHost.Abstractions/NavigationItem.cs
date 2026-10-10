namespace BlazorHost.Abstractions;

// --8<-- [start:navigation]
/// <summary>
/// An entry in the host's navigation menu. Plugins register entries as services; the menu shows all of them.
/// </summary>
public sealed record NavigationItem(string Title, string Href);
// --8<-- [end:navigation]
