using Azure;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using ElAgentApi.Bot.Models;
using ElAgentApi.Bot.Plugins;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;


namespace ElAgentApi.Bot.Agents
{
    /// <summary>
    /// This class represents a SharePoint agent that can interact with Azure AI Agents to process chat messages.
    /// This agent uses OBO to call Azure AI Foundry Agent that uses the sharepoint knowledge source.
    /// </summary>
    public class SharepointAgent
    {
        private readonly ITurnContext turnContext;
        private readonly ITurnState turnState;
        private readonly string accessToken;
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        private PersistentAgentsClient agentsClient;
        private string foundryProjectEndpoint;

        public SharepointAgent(IConfiguration configuration, ITurnContext turnContext, ITurnState turnState, string accessToken)
        {
            this.foundryProjectEndpoint = configuration.GetValue<string>("AIProjectConnectionString")!;
            agentsClient = AzureAIAgent.CreateAgentsClient(foundryProjectEndpoint, new DefaultAzureCredential());
            this.turnContext = turnContext;
            this.turnState = turnState;
            this.accessToken = accessToken;
        }

        public async Task InvokeAgentAsync(ChatMessageContent chatMessage, CancellationToken cancellationToken)
        {
            string agentId = "asst_Di4116mzcGWtqIYyVVOytivd";
            PersistentAgent agent = agentsClient.Administration.GetAgent(agentId);
            PersistentAgentThread? thread = null;
            var fileReferences = new List<FileReference>();
            var citations = new List<Citation>();
            var quote = string.Empty;
            ChatHistory chatHistory = turnState.GetValue("conversation.chatHistory", () => new ChatHistory());

            var threadId = turnState.GetValue("conversation.SharepointThreadId", () => string.Empty);
            if (!string.IsNullOrEmpty(threadId))
            {
                // get existing agent thread
                thread = await agentsClient.Threads.GetThreadAsync(threadId);
            }
            else
            {
                thread = agentsClient.Threads.CreateThread();
                turnState.SetValue("conversation.SharepointThreadId", thread.Id);
            }

            try
            {
                PersistentThreadMessage messageResponse = agentsClient.Messages.CreateMessage(
                    thread.Id,
                    MessageRole.User,
                    chatMessage.Content);

                // Example call to Azure AI Foundry Agents REST (create a thread)
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var apiResponse = await http.PostAsJsonAsync($"{foundryProjectEndpoint}/threads/{thread.Id}/runs?api-version=2025-05-01", new { assistant_id = agentId });

                if (!apiResponse.IsSuccessStatusCode)
                {
                    var econtent = await apiResponse.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Failed to create run: {apiResponse.ReasonPhrase}");
                }
                var content = await apiResponse.Content.ReadAsStringAsync();
                string id;
                string status;
                using (JsonDocument doc = JsonDocument.Parse(content))
                {
                    id = doc.RootElement.GetProperty("id").GetString();
                    status = doc.RootElement.GetProperty("status").GetString();
                }

                //Poll until the run reaches a terminal status
                do
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200));
                    var run = await http.GetAsync($"{foundryProjectEndpoint}/threads/{thread.Id}/runs/{id}?api-version=2025-05-01");

                    if (run.IsSuccessStatusCode)
                    {
                        var runStatus = await run.Content.ReadAsStringAsync();
                        using (JsonDocument doc = JsonDocument.Parse(runStatus))
                        {
                            id = doc.RootElement.GetProperty("id").GetString();
                            status = doc.RootElement.GetProperty("status").GetString();
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Failed to get run status: {run.ReasonPhrase}");
                    }
                }
                while (status.Equals(RunStatus.Queued.ToString(), StringComparison.InvariantCultureIgnoreCase)
                    || status.Equals(RunStatus.InProgress.ToString(), StringComparison.InvariantCultureIgnoreCase));
                if (!status.Equals(RunStatus.Completed.ToString(), StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new InvalidOperationException($"Run failed or was canceled:");
                }


                Pageable<PersistentThreadMessage> messages = agentsClient.Messages.GetMessages(
                thread.Id, order: ListSortOrder.Ascending);

                StringBuilder sb = new StringBuilder();
                // Display messages
                var latestAgentMessage = messages.LastOrDefault(m => m.Role == MessageRole.Agent);
                if (latestAgentMessage == null)
                {
                    turnContext.StreamingResponse.QueueTextChunk("I'm sorry, I couldn't find any information on that topic.");
                    return;
                }

                foreach (MessageContent contentItem in latestAgentMessage.ContentItems)
                {
                    if (contentItem is MessageTextContent textItem)
                    {
                        string response = textItem.Text;
                        if (string.IsNullOrEmpty(response))
                        {
                            continue;
                        }
                        //https://github.com/azure-ai-foundry/foundry-samples/blob/main/samples/microsoft/csharp/getting-started-agents/BingGrounding/BingGrounding.md
                        if (textItem.Annotations != null)
                        {
                            foreach (MessageTextAnnotation annotation in textItem.Annotations)
                            {
                                if (annotation is MessageTextUriCitationAnnotation urlAnnotation)
                                {
                                    response = response.Replace(urlAnnotation.Text, $" [{urlAnnotation.UriCitation.Title}] ({urlAnnotation.UriCitation.Uri})");
                                }
                            }
                        }
                        turnContext.StreamingResponse.QueueTextChunk(response);
                    }
                    else if (contentItem is MessageImageFileContent imageFileItem)
                    {
                        sb.Append($"<image from ID: {imageFileItem.FileId}");
                    }
                    //sb.AppendLine();
                }

                // add citations
                //foreach (var fileReference in fileReferences)
                //{
                //    citations.Add(fileReference.Citation);
                //}
                //turnContext.StreamingResponse.AddCitations(citations);

            }
            catch (Exception ex)
            {
                // handle any exceptions that occur during the agent invocation
                turnContext.StreamingResponse.QueueTextChunk($"An error occurred while processing your request: {ex.Message}");
            }

        }

        //write clean up method to remove the agent thread from the turn state
        public async Task CleanUp()
        {
            var threadId = turnState.GetValue("conversation.OfferingThreadId", () => string.Empty);
            if (!string.IsNullOrEmpty(threadId))
            {
                await agentsClient.Threads.DeleteThreadAsync(threadId);
            }
        }
    }
}
