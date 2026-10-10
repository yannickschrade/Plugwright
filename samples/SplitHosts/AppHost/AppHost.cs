#region apphost
var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.SplitHosts_Api>("api");

builder.AddProject<Projects.SplitHosts_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
#endregion
