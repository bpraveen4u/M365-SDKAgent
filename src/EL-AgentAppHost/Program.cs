using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.EL_Agent_Api>("Api");

builder.Build().Run();
