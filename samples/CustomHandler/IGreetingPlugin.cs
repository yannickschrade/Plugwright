namespace CustomHandler;

// --8<-- [start:contract]
/// <summary>
/// A kind of plugin the host defines itself: it contributes greetings instead of configuring services.
/// </summary>
public interface IGreetingPlugin
{
    IEnumerable<string> GetGreetings();
}
// --8<-- [end:contract]
