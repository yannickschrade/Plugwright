using Microsoft.AspNetCore.Mvc;

namespace Acme.Greetings;

// --8<-- [start:controller]
/// <summary>
/// A controller in the plugin's assembly. The web plugin handler makes it known to MVC.
/// </summary>
[ApiController]
[Route("api/greetings")]
public sealed class GreetingsController(IGreeter greeter) : ControllerBase
{
    [HttpGet]
    public string Get(string name = "World") => greeter.Greet(name);
}
// --8<-- [end:controller]
