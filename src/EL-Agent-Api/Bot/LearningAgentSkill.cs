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
    public class LearningAgentSkill : AgentApplication
    {
        private Kernel kernel;
        private readonly IConfiguration configuration;
        private ServiceProvider? serviceProvider;

        public LearningAgentSkill(AgentApplicationOptions options, Kernel kernel, IConfiguration configuration) : base(options)
        {
            this.kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            this.configuration = configuration;
            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
            
            OnActivity(ActivityTypes.EndOfConversation, EndOfConversationAsync);
            OnActivity(ActivityTypes.Message, MessageActivityAsync, rank: RouteRank.Last);
            OnTurnError(async (turnContext, turnState, exception, cancellationToken) =>
            {
                await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);

                var eoc = Activity.CreateEndOfConversationActivity();
                eoc.Code = EndOfConversationCodes.Error;
                eoc.Text = exception.Message;
                await turnContext.SendActivityAsync(eoc, cancellationToken);
            });
        }

        protected async Task EndOfConversationAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Thank you for the conversation. Goodbye!"), cancellationToken);
            await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
            //Clean up the conversation state
            //if (serviceProvider != null)
            //{
            //    var agent = serviceProvider.GetService<OfferingsAgent>();
            //    await agent?.CleanUp();
            //}            
        }

        protected async Task MessageActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            // Setup local service connection
            ServiceCollection serviceCollection = [
                new ServiceDescriptor(typeof(ITurnState), turnState),
                new ServiceDescriptor(typeof(ITurnContext), turnContext),
                new ServiceDescriptor(typeof(Kernel), kernel),
                new ServiceDescriptor(typeof(OfferingsAgent), sp => new OfferingsAgent(this.configuration, turnContext, turnState), ServiceLifetime.Singleton),
            ];

            serviceProvider = serviceCollection.BuildServiceProvider();

            if(turnContext.Activity.Text.Equals("bye", StringComparison.OrdinalIgnoreCase) || turnContext.Activity.Text.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Thank you for the conversation. Goodbye!"), cancellationToken);

                //Getting error!
                //var endActivity = Activity.CreateEndOfConversationActivity();
                //endActivity.Code = EndOfConversationCodes.CompletedSuccessfully;
                //await turnContext.SendActivityAsync(endActivity, cancellationToken);
            }
            else
            {
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
