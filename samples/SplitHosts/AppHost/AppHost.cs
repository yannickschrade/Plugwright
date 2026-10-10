// --8<-- [start:apphost]
var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.SplitHosts_Api>("api");

// Uncomment to turn off the backend part of the orders feature; the web host then hides the UI part.
// api.WithEnvironment("Plugwright__Plugins__Acme.Orders__Enabled", "false");

builder.AddProject<Projects.SplitHosts_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
// --8<-- [end:apphost]
