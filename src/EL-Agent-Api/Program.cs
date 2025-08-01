using EL_Agent_Api.Bot.Plugins;
using ElAgentApi;
using ElAgentApi.Bot.Agents;
using ElAgentApi.Bot.Plugins;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MyM365Agent1;

var builder = WebApplication.CreateBuilder(args);
AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

builder.AddServiceDefaults();
builder.AddAzureOpenAIClient("azureOpenAI");
builder.Services.AddControllers();

builder.Services.AddHttpClient("WebClient", client => client.Timeout = TimeSpan.FromSeconds(600));
builder.Services.AddHttpContextAccessor();
builder.Logging.AddConsole();

// Register Semantic Kernel
builder.Services.AddKernel().AddAzureOpenAIChatCompletion("gpt-4");


// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add AgentApplicationOptions from config.
builder.AddAgentApplicationOptions();

//Copilot studio connection settings
// Get the configuration settings for the DirectToEngine client from the appsettings.json file.
SampleConnectionSettings settings = new SampleConnectionSettings(builder.Configuration.GetSection("CopilotStudioClientSettings"));
builder.Services.AddSingleton(settings)
    .AddTransient<CopilotClient>((s) =>
    {
        var logger = s.GetRequiredService<ILoggerFactory>().CreateLogger<CopilotClient>();
        return new CopilotClient(settings, s.GetRequiredService<IHttpClientFactory>(), logger, "mcs");
    });

// Add the bot (which is transient)
builder.AddAgent<ElAgentApi.Bot.LearningAgentSkill>();

// Add AspNet token validation
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

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
}).RequireAuthorization();

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



