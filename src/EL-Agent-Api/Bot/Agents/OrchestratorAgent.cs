using EL_Agent_Api.Bot.Plugins;
using ElAgentApi.Bot.Plugins;
using Microsoft.Agents.Builder;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;

namespace ElAgentApi.Bot.Agents;

public class OrchestratorAgent
{
    private readonly Kernel kernel;
    private readonly IServiceProvider service;
    private readonly ChatCompletionAgent agent;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestratorAgent"/> class.
    /// </summary>
    /// <param name="kernel">An instance of <see cref="Kernel"/> for interacting with an LLM.</param>
    public OrchestratorAgent(Kernel kernel, IServiceProvider service)
    {
        this.kernel = kernel;
        this.service = service;

        // Define the agent
        agent =
            new(ReadFileForPromptTemplateConfig("./Bot/Agents/Prompts/Agent.yaml"), templateFactory: new KernelPromptTemplateFactory())
            {
                Kernel = this.kernel,
                Arguments = new KernelArguments(new OpenAIPromptExecutionSettings() 
                { 
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(), 
                    //ResponseFormat = "json_object",
                    Temperature = 0.1,
                    MaxTokens = 500,
                }),
            };

        // Give the agent some tools to work with
        agent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromType<DateTimePlugin>(serviceProvider: service));
        agent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromType<OfferingsPlugin>(serviceProvider: service));
        agent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromType<MathPlugin>(serviceProvider: service));
    }

    /// <summary>
    /// Invokes the agent with the given input and returns the response.
    /// </summary>
    /// <param name="input">A message to process.</param>
    /// <returns>An instance of <see cref="WeatherForecastAgentResponse"/></returns>
    public async Task InvokeAgentAsync(ChatMessageContent message, ChatHistory chatHistory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatHistory);
        chatHistory.Add(message);
        AgentThread thread = new ChatHistoryAgentThread();
        var turnContext = service.GetService<ITurnContext>();

        await foreach (var response in this.agent.InvokeStreamingAsync(chatHistory, thread: thread, cancellationToken: cancellationToken))
        {
            if (response.Message.Content is null)
            {
                continue;
            }

            chatHistory.Add(new ChatMessageContent(AuthorRole.Assistant, response.Message.Content));
            turnContext!.StreamingResponse.QueueTextChunk(response.Message.Content!);
        }

        // Make sure the response is in the correct format and retry if necessary
        await turnContext!.StreamingResponse.EndStreamAsync(cancellationToken);
    }

    static PromptTemplateConfig ReadFileForPromptTemplateConfig(string fileName)
    {
        string yaml = File.ReadAllText(fileName);
        return KernelFunctionYaml.ToPromptTemplateConfig(yaml);
    }
}
