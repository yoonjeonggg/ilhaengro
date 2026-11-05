var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Ilhaengro_Api>("api")
    .WithHttpHealthCheck("/health", endpointName: "http")
    .WithExternalHttpEndpoints();

var web = builder.AddViteApp("web", "../..")
    .WithReference(api)
    .WaitFor(api);

api.PublishWithContainerFiles(web, "wwwroot");

builder.Build().Run();
