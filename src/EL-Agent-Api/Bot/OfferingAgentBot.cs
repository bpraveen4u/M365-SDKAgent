using Azure;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ElAgentApi.Bot.Agents;
using Microsoft.SemanticKernel.Agents;

namespace ElAgentApi.Bot
{
    public class OfferingAgentBot : AgentApplication
    {
        private Kernel kernel;
        private readonly IConfiguration configuration;

        public OfferingAgentBot(AgentApplicationOptions options, Kernel kernel, IConfiguration configuration) : base(options)
        {
            this.kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            this.configuration = configuration;
            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
            OnActivity(ActivityTypes.Message, MessageActivityAsync, rank: RouteRank.Last);
        }

        private async Task MessageActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            // Setup local service connection
            ServiceCollection serviceCollection = [
                new ServiceDescriptor(typeof(ITurnState), turnState),
                new ServiceDescriptor(typeof(ITurnContext), turnContext),
                new ServiceDescriptor(typeof(Kernel), kernel),
                new ServiceDescriptor(typeof(OfferingsAgent), sp => new OfferingsAgent(this.configuration, turnContext, turnState), ServiceLifetime.Singleton),
            ];

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Start a Streaming Process 
            await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Working on a response for you", cancellationToken);

            // Create the Orchestrator Agent
            var orchestratorAgent = new OrchestratorAgent(kernel, serviceCollection.BuildServiceProvider());

            // create the chat message to send to the agent
            var message = new ChatMessageContent(AuthorRole.User, turnContext.Activity.Text);

            // Start a Streaming Process 
            await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Working on a response for you", cancellationToken);

            ChatHistory chatHistory = turnState.GetValue("conversation.chatHistory", () => new ChatHistory());
            await orchestratorAgent.InvokeAgentAsync(message, chatHistory, cancellationToken);
        }

        private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome! I'm here to help with all your questions!"), cancellationToken);
                }
            }
        }
    }
}
