namespace CustomHandler;

#region plugins
public sealed class EnglishGreetings : IGreetingPlugin
{
    public IEnumerable<string> GetGreetings() => ["Hello", "Good morning"];
}

public sealed class GermanGreetings : IGreetingPlugin
{
    public IEnumerable<string> GetGreetings() => ["Hallo", "Guten Morgen"];
}
#endregion
