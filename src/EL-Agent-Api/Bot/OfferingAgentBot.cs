using Azure;
using EL_Agent_Api.Bot.Tools;
using ElAgentApi.Bot.Agents;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace ElAgentApi.Bot
{
    public class OfferingAgentBot : AgentApplication
    {
        OfferingsAgent _offeringsAgent;
        private readonly PlaywriteMcpClient mcpClient;
        private Kernel _kernel;
        private readonly IConfiguration configuration;

        public OfferingAgentBot(AgentApplicationOptions options, Kernel kernel, IConfiguration configuration, OfferingsAgent offeringsAgent, PlaywriteMcpClient mcpClient) : base(options)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            this.configuration = configuration;
            _offeringsAgent = offeringsAgent;
            this.mcpClient = mcpClient;
            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
            OnActivity(ActivityTypes.Message, MessageActivityAsync, rank: RouteRank.Last);
        }

        private async Task MessageActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            // Setup local service connection
            ServiceCollection serviceCollection = [
                new ServiceDescriptor(typeof(ITurnState), turnState),
                new ServiceDescriptor(typeof(ITurnContext), turnContext),
                new ServiceDescriptor(typeof(Kernel), _kernel),
            ];

            // create the chat message to send to the agent
            var message = new ChatMessageContent(AuthorRole.User, turnContext.Activity.Text);

            // Start a Streaming Process 
            await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Working on a response for you", cancellationToken);

            ChatHistory chatHistory = turnState.GetValue("conversation.chatHistory", () => new ChatHistory());

            //await _offeringsAgent.InvokeAgentAsync(turnContext.Activity.Text, turnContext, chatHistory, cancellationToken);

            var tools = await mcpClient.GetAvailableToolsAsync();
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            _kernel.Plugins.AddFromFunctions("GitHub", tools.Select(aiFunction => aiFunction.AsKernelFunction()));
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            //if (tools != null && tools.Any())
            //{
            //    var toolList = string.Join(", ", tools.Select(t => t.Name));
            //    turnContext.StreamingResponse.QueueTextChunk($"Available tools: {toolList}");
            //}
            //else
            //{
            //    turnContext.StreamingResponse.QueueTextChunk("No tools available at the moment.");
            //}

            // Enable automatic function calling
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            OpenAIPromptExecutionSettings executionSettings = new()
            {
                Temperature = 0,
                //ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
            };
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            // Add the user message to the chat history
            var result = await _kernel.InvokePromptAsync(turnContext.Activity.Text, new(executionSettings)).ConfigureAwait(false);

            
            if (result != null)
            {

                turnContext.StreamingResponse.QueueTextChunk(result.ToString());
            }
            else
            {
                turnContext.StreamingResponse.QueueTextChunk("No result returned from the agent.");
            }

            // Invoke the WeatherForecastAgent to process the message
            //await foreach (StreamingChatMessageContent response in _offeringsAgent.InvokeAgentAsync(turnContext.Activity.Text, chatHistory))
            //{
            //    if (response == null)
            //    {
            //        turnContext.StreamingResponse.QueueTextChunk("Sorry, I couldn't get the offerings at the moment.");
            //        await turnContext.StreamingResponse.EndStreamAsync(cancellationToken);
            //        return;
            //    }

            //    chatHistory.Add(response);
            //    turnContext.StreamingResponse.QueueTextChunk(response.Content);
            //}

            ////turnContext.StreamingResponse.QueueTextChunk(GetVideoAdaptiveCard());
            //turnContext.StreamingResponse.FinalMessage = MessageFactory.Attachment(new Attachment()
            //{
            //    ContentType = "application/vnd.microsoft.card.adaptive",
            //    Content = GetVideoAdaptiveCard(),
            //});

            await turnContext.StreamingResponse.EndStreamAsync(cancellationToken); // End the streaming response
        }

        private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome! I'm here to help with all your offerings and program needs!"), cancellationToken);
                }
            }
        }

        private string GetVideoAdaptiveCard()
        {
            // This method can be used to return an adaptive card for video content.
            // For now, it returns a placeholder string.
            return """
            {
              "type": "AdaptiveCard",
              "version": "1.5",
              "body": [
                {
                  "type": "Media",
                   "altText": "This is a test video",
                    "poster": "https://adaptivecards.microsoft.com/images/adaptivecards1.jpeg",
                    "id": "media",
                    "sources": [
                        {
                            "mimeType": "video/mp4",
                            "url": "https://employeehub-my.sharepoint.com/:v:/g/personal/prbona_employeehub_onmicrosoft_com/ETG4mZkmvspKg2ap8YcAl10BnTC9Gc7zQBx0CD76GGj0Ig?e=21bggE&nav=eyJyZWZlcnJhbEluZm8iOnsicmVmZXJyYWxBcHAiOiJTdHJlYW1XZWJBcHAiLCJyZWZlcnJhbFZpZXciOiJTaGFyZURpYWxvZy1MaW5rIiwicmVmZXJyYWxBcHBQbGF0Zm9ybSI6IldlYiIsInJlZmVycmFsTW9kZSI6InZpZXcifX0%3D"
                        }
                    ]
                },
                {
                  "type": "TextBlock",
                  "text": "Telegraph Road - Dire Straits",
                  "size": "Large",
                  "weight": "Bolder",
                  "wrap": true
                },
                {
                  "type": "TextBlock",
                  "spacing": "None",
                  "text": "Cover by David Claux",
                  "wrap": true
                }
              ]
            }
            """;
        }
    }
}
