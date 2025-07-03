using EL_Agent_Api.Bot.Plugins;
using ElAgentApi;
using ElAgentApi.Bot.Agents;
using ElAgentApi.Bot.Plugins;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;

var builder = WebApplication.CreateBuilder(args);
AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

builder.AddServiceDefaults();
builder.AddAzureOpenAIClient("azureOpenAI");
builder.Services.AddControllers();
builder.Services.AddHttpClient("WebClient", client => client.Timeout = TimeSpan.FromSeconds(600));
builder.Services.AddHttpContextAccessor();
builder.Logging.AddConsole();


//builder.Services.AddSingleton<MathPlugin>();
//builder.Services.AddSingleton<OfferingsPlugin>();
//builder.Services.AddSingleton<OfferingsAgent>();

// Register Semantic Kernel
builder.Services.AddKernel().AddAzureOpenAIChatCompletion("gpt-4");

//Orchestrator Agent
//builder.Services.AddSingleton(builder =>
//{
//    var _settings = new OpenAIPromptExecutionSettings()
//    {
//        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
//        Temperature = 0.1,
//        MaxTokens = 500,
//    };
//    ChatCompletionAgent agent = new (ReadFileForPromptTemplateConfig("./Bot/Agents/Prompts/Agent.yaml"), templateFactory: new KernelPromptTemplateFactory())
//    {
//        Kernel = builder.GetRequiredService<Kernel>(),
//        Arguments = new(_settings)
//    };
//    agent.Kernel.Plugins.AddFromObject(builder.GetRequiredService<MathPlugin>());
//    agent.Kernel.Plugins.AddFromObject(builder.GetRequiredService<OfferingsPlugin>());

//    return agent;
//});


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


