using Azure;
using EL_Agent_Api.Bot.Agents;
using ElAgentApi.Bot.Agents;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace ElAgentApi.Bot
{
    public class LearningAgentSkill : AgentApplication
    {
        private Kernel kernel;
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;
        private ServiceProvider? serviceProvider;

        /// <summary>
        /// Default Sign In Name
        /// </summary>
        private string _defaultDisplayName = "Unknown User";

        public LearningAgentSkill(AgentApplicationOptions options, Kernel kernel, IConfiguration configuration, IHttpClientFactory httpClientFactory) : base(options)
        {
            this.kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            this.configuration = configuration;
            this.httpClientFactory = httpClientFactory;
            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
            
            OnActivity(ActivityTypes.EndOfConversation, EndOfConversationAsync);
            OnActivity(ActivityTypes.Message, MessageActivityAsync, rank: RouteRank.Last, autoSignInHandlers: ["auto", "aai", "mcs"]);
            OnTurnError(async (turnContext, turnState, exception, cancellationToken) =>
            {
                await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);

                var eoc = Activity.CreateEndOfConversationActivity();
                eoc.Code = EndOfConversationCodes.Error;
                eoc.Text = exception.Message;
                await turnContext.SendActivityAsync(eoc, cancellationToken);
            });

            UserAuthorization.OnUserSignInFailure(OnUserSignInFailure);
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
            var foundryToken = await UserAuthorization.ExchangeTurnTokenAsync(turnContext, "aai");
            //var mcsToken = await UserAuthorization.ExchangeTurnTokenAsync(turnContext, "mcs");
            var mcsClient = GetClient(turnContext);
            // Setup local service connection
            ServiceCollection serviceCollection = [
                new ServiceDescriptor(typeof(ITurnState), turnState),
                new ServiceDescriptor(typeof(ITurnContext), turnContext),
                new ServiceDescriptor(typeof(Kernel), kernel),
                new ServiceDescriptor(typeof(OfferingsAgent), sp => new OfferingsAgent(this.configuration, turnContext, turnState), ServiceLifetime.Singleton),
                new ServiceDescriptor(typeof(SharepointAgent), sp => new SharepointAgent(this.configuration, turnContext, turnState, foundryToken), ServiceLifetime.Singleton),
                new ServiceDescriptor(typeof(SimpleCopilotAgent), sp => new SimpleCopilotAgent(this.configuration, turnContext, turnState, mcsClient), ServiceLifetime.Singleton),
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

        CopilotClient GetClient(ITurnContext turnContext)
        {
            var settings = new ConnectionSettings(configuration.GetSection("CopilotStudioClientSettings"));
            string[] scopes = [CopilotClient.ScopeFromSettings(settings)];
            var token = UserAuthorization.ExchangeTurnTokenAsync(turnContext, "mcs", exchangeScopes: scopes).Result;
            return new CopilotClient(
                settings,
                httpClientFactory,
                tokenProviderFunction: async (s) =>
                {
                    // In this sample, the Azure Bot OAuth Connection is configured to return an 
                    // exchangeable token, that can be exchange for different scopes.  This can be
                    // done multiple times using different scopes.
                    return await UserAuthorization.ExchangeTurnTokenAsync(turnContext, "mcs", exchangeScopes: scopes);
                },
                NullLogger.Instance,
                "mcs");
        }

        private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    string displayName = await GetDisplayName(turnContext);
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Hello and Welcome, {displayName}! I'm here to help with all your questions!"), cancellationToken);
                }
            }
        }

        /// <summary>
        /// This method is called when the sign-in process fails with an error indicating why . 
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="turnState"></param>
        /// <param name="handlerName"></param>
        /// <param name="response"></param>
        /// <param name="initiatingActivity"></param>
        /// <param name="cancellationToken"></param>
        private async Task OnUserSignInFailure(ITurnContext turnContext, ITurnState turnState, string handlerName, SignInResponse response, IActivity initiatingActivity, CancellationToken cancellationToken)
        {
            // Raise a notification to the user that the sign-in process failed.  In a production Agent, this would be used
            // to display alternative ways to get help, or in some cases transfer to a live agent.
            await turnContext.SendActivityAsync($"Sign In: Failed to login to '{handlerName}': {response.Cause}/{response.Error.Message}", cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Gets the display name of the user from the Graph API using the access token.
        /// </summary>
        private async Task<string> GetDisplayName(ITurnContext turnContext)
        {
            string displayName = _defaultDisplayName;
            var graphInfo = await GetGraphInfo(turnContext, UserAuthorization.DefaultHandlerName);
            if (graphInfo != null)
            {
                displayName = graphInfo!["displayName"].GetValue<string>();
            }
            return displayName;
        }

        private async Task<JsonNode> GetGraphInfo(ITurnContext turnContext, string handleName)
        {
            string accessToken = await UserAuthorization.GetTurnTokenAsync(turnContext, handleName);
            string graphApiUrl = $"https://graph.microsoft.com/v1.0/me";
            try
            {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                HttpResponseMessage response = await client.GetAsync(graphApiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonNode.Parse(content);
                }
            }
            catch (Exception ex)
            {
                // Handle error response from Graph API
                System.Diagnostics.Trace.WriteLine($"Error getting display name: {ex.Message}");
            }
            return null;
        }
    }
}
