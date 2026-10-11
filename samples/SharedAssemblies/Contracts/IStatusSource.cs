namespace SharedAssemblies.Contracts;

// --8<-- [start:contract]
/// <summary>
/// Something that reports its status on the host's status page. Plugins register implementations as services.
/// </summary>
public interface IStatusSource
{
    string Name { get; }

    string GetStatus();
}
// --8<-- [end:contract]
