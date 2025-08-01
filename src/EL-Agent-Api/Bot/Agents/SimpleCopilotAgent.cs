using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Extensions.Logging.Abstractions;

namespace EL_Agent_Api.Bot.Agents
{
    public class SimpleCopilotAgent
    {
        const string MCSConversationPropertyName = "MCSConversationId";
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly CopilotClient copilotClient;

        public SimpleCopilotAgent(IConfiguration configuration, IHttpClientFactory httpClientFactory, CopilotClient copilotClient)
        {
            this.configuration = configuration;
            this.httpClientFactory = httpClientFactory;
            this.copilotClient = copilotClient;
        }
        CopilotClient GetClient(AgentApplication app, ITurnContext turnContext)
        {
            var settings = new ConnectionSettings(configuration.GetSection("CopilotStudioAgent"));
            string[] scopes = [CopilotClient.ScopeFromSettings(settings)];

            return new CopilotClient(
                settings,
                httpClientFactory,
                tokenProviderFunction: async (s) =>
                {
                    // In this sample, the Azure Bot OAuth Connection is configured to return an 
                    // exchangeable token, that can be exchange for different scopes.  This can be
                    // done multiple times using different scopes.
                    return await app.UserAuthorization.ExchangeTurnTokenAsync(turnContext, "mcs", exchangeScopes: scopes);
                },
                NullLogger.Instance,
                "mcs");
        }
    }
}
