using Azure;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using ElAgentApi.Bot.Models;
using ElAgentApi.Bot.Plugins;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Configuration;
using System.Text;
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
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        //private readonly AzureAIAgent aiAgent;

        private PersistentAgentsClient agentsClient;

        public SharepointAgent(IConfiguration configuration, ITurnContext turnContext, ITurnState turnState)
        {
            agentsClient = AzureAIAgent.CreateAgentsClient(configuration.GetValue<string>("AIProjectConnectionString")!, new DefaultAzureCredential());
            //var agent = agentsClient.Administration.GetAgent("asst_Di4116mzcGWtqIYyVVOytivd");
            //aiAgent = new(agent, agentsClient);
            this.turnContext = turnContext;
            this.turnState = turnState;
        }

        public async Task InvokeAgentAsync(ChatMessageContent chatMessage, CancellationToken cancellationToken)
        {
            PersistentAgent agent = agentsClient.Administration.GetAgent("asst_Di4116mzcGWtqIYyVVOytivd");
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

                ThreadRun run = agentsClient.Runs.CreateRun(thread.Id, agent.Id);

                // Poll until the run reaches a terminal status
                do
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                    run = agentsClient.Runs.GetRun(thread.Id, run.Id);
                }
                while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);
                
                if (run.Status != RunStatus.Completed)
                {
                    throw new InvalidOperationException($"Run failed or was canceled: {run.LastError?.Message}");
                }

                Pageable<PersistentThreadMessage> messages = agentsClient.Messages.GetMessages(
                thread.Id, order: ListSortOrder.Ascending);

                StringBuilder sb = new StringBuilder();
                // Display messages
                foreach (PersistentThreadMessage threadMessage in messages)
                {
                    foreach (MessageContent contentItem in threadMessage.ContentItems)
                    {
                        if (contentItem is MessageTextContent textItem)
                        {
                            string response = textItem.Text;
                            if (response.Equals(chatMessage.Content, StringComparison.InvariantCultureIgnoreCase))
                            {
                                continue; // Skip null text items
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

                            sb.AppendLine(response);
                        }
                        else if (contentItem is MessageImageFileContent imageFileItem)
                        {
                            sb.Append($"<image from ID: {imageFileItem.FileId}");
                        }
                        sb.AppendLine();
                    }
                }

                //TODO: invoke agent the stream
                turnContext.StreamingResponse.QueueTextChunk(sb.ToString());

                //await foreach (StreamingChatMessageContent chunk in aiAgent.InvokeStreamingAsync(chatMessage, agentThread, cancellationToken: cancellationToken))
                //{
                //    if (string.IsNullOrEmpty(threadId))
                //    {
                //        threadId = agentThread.Id;
                //        turnState.SetValue("conversation.OfferingThreadId", threadId!);
                //    }

                //    // get the annotation content from the message chunk items, if there are any
                //    var annotations = chunk.Items.OfType<StreamingAnnotationContent>();

                //    foreach (StreamingAnnotationContent annotation in annotations)
                //    {
                //        // check if the file reference already exists in the list and skip it if it does
                //        if (fileReferences.Any(fr => fr.Quote == annotation.Label)) { continue; }

                //        var agentFile = await aiAgent.Client.Files.GetFileAsync(annotation.ReferenceId, cancellationToken);
                //        var citation = new Citation(string.Empty, agentFile.Value.Filename, "https://m365.cloud.microsoft/chat");

                //        var fileReference = new FileReference(agentFile.Value.Id, agentFile.Value.Filename, annotation.Label, citation);
                //        fileReferences.Add(fileReference);
                //    }

                //    // if the message chunk content is empty, we can skip it
                //    // this happens when the chunk contains StreamingAnnotationContent items
                //    if (chunk.Content == null) { continue; }

                //    // if the previous message chunk contained the citation quote, we can process it now
                //    if (quote != string.Empty)
                //    {
                //        var fileReferenceIndex = fileReferences.FindIndex(fr => fr.Quote == quote);
                //        //turnContext.StreamingResponse.QueueTextChunk($" [{fileReferenceIndex + 1}] ");

                //        // reset the quote to empty string to avoid processing it again
                //        quote = string.Empty;
                //        continue;
                //    }

                //    // if the message chunk contains an annotation quote 【4:0†source】
                //    // store the value for the next message chunk so we can process it
                //    // we don't want to send it to the user yet
                //    if (chunk.Content.Contains('【'))
                //    {
                //        quote = chunk.Content;
                //        continue;
                //    }
                //    else
                //    {
                //        // just a regular message chunk, we can send it to the user
                //        turnContext.StreamingResponse.QueueTextChunk(chunk.Content);
                //    }

                //    //chatHistory.Add(chunk.Content);
                //    //yield return response;
                //}

                // add citations
                foreach (var fileReference in fileReferences)
                {
                    citations.Add(fileReference.Citation);
                }
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
