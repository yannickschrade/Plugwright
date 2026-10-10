namespace Acme.Greetings;

/// <summary>
/// The service the plugin contributes. The host uses it in its own endpoint.
/// </summary>
public interface IGreeter
{
    string Greet(string name);
}

internal sealed class Greeter(string salutation) : IGreeter
{
    public string Greet(string name) => $"{salutation}, {name}!";
}
