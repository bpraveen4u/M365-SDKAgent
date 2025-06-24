using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using ElAgentApi.Bot.Plugins;
using System.Configuration;
using System.Text;


namespace ElAgentApi.Bot.Agents
{
    public class OfferingsAgent
    {
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        AzureAIAgent aiAgent;

        private PersistentAgentsClient agentsClient;

        public OfferingsAgent(IConfiguration configuration)
        {
            agentsClient = AzureAIAgent.CreateAgentsClient(configuration.GetValue<string>("AIProjectConnectionString")!, new DefaultAzureCredential());
            var agent = agentsClient.Administration.GetAgent("asst_YK2flcJLkjtQBgnEC9qkJsiN");
            aiAgent = new(agent, agentsClient);
        }

        public async IAsyncEnumerable<ChatMessageContent> InvokeAgentAsync(string input, ChatHistory chatHistory)
        {
            AzureAIAgentThread agentThread = new(aiAgent.Client);
            
            ChatMessageContent message = new(AuthorRole.User, input);
            await foreach (ChatMessageContent response in aiAgent.InvokeAsync(message, agentThread))
            {
                chatHistory.Add(response);
                yield return response;
            }
        }
    }
}
