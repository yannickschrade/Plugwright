using Microsoft.Extensions.DependencyInjection;
using Plugwright;

namespace Acme.Formatting;

#region formatting
// A service that other plugins can use.
public interface IGreetingFormatter
{
    string Format(string name);
}

public sealed class FormattingPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services.AddSingleton<IGreetingFormatter, GreetingFormatter>();
    }
}
#endregion

internal sealed class GreetingFormatter : IGreetingFormatter
{
    public string Format(string name) => $"Hello, {name}!";
}
