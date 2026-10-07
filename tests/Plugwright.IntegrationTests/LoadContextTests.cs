namespace Plugwright.IntegrationTests;

/// <summary>
/// Tests that create assembly load contexts run one after another.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LoadContextTests
{
    public const string Name = "Load contexts";
}
