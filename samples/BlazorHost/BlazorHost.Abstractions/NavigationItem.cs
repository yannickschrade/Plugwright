namespace BlazorHost.Abstractions;

#region navigation
/// <summary>
/// An entry in the host's navigation menu. Plugins register entries as services; the menu shows all of them.
/// </summary>
public sealed record NavigationItem(string Title, string Href);
#endregion
