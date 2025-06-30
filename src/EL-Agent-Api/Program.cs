using EL_Agent_Api.Bot.Tools;
using ElAgentApi;
using ElAgentApi.Bot.Agents;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.SemanticKernel;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient("WebClient", client => client.Timeout = TimeSpan.FromSeconds(600));
builder.Services.AddHttpContextAccessor();
builder.Logging.AddConsole();


// Register Semantic Kernel
builder.Services.AddKernel();

// Register the AI service of your choice. AzureOpenAI and OpenAI are demonstrated...
var config = builder.Configuration.Get<ConfigOptions>();

builder.Services.AddAzureOpenAIChatCompletion(
    deploymentName: config.Azure.OpenAIDeploymentName,
    endpoint: config.Azure.OpenAIEndpoint,
    credentials: new DefaultAzureCredential()
);

/// Create an MCPClient for the GitHub server
//await using var mcpClient = await McpClientFactory.CreateAsync(new StdioClientTransport(new()
//{
//    Name = "MCPServer",
//    Command = "npx",
//    Arguments = ["-y", "@modelcontextprotocol/server-github"],
//}));

// Retrieve the list of tools available on the GitHub server
//var tools = await mcpClient.ListToolsAsync().ConfigureAwait(false);
//foreach (var tool in tools)
//{
//    Console.WriteLine($"{tool.Name}: {tool.Description}");
//}
// Register the MCP client as a singleton service

builder.Services.AddSingleton<PlaywriteMcpClient>();
builder.Services.AddSingleton<OfferingsAgent>();


//builder.Services.AddSingleton<IMcpClient>(mcpClient);
// Add AspNet token validation
builder.Services.AddBotAspNetAuthentication(builder.Configuration);

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add AgentApplicationOptions from config.
builder.AddAgentApplicationOptions();

// Add AgentApplicationOptions.  This will use DI'd services and IConfiguration for construction.
builder.Services.AddTransient<AgentApplicationOptions>();

// Add the bot (which is transient)
builder.AddAgent<ElAgentApi.Bot.OfferingAgentBot>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Playground")
{
    app.MapGet("/", () => "Learning Bot");
    app.UseDeveloperExceptionPage();
    app.MapControllers().AllowAnonymous();
}
else
{
    app.MapControllers();
}

app.Run();

