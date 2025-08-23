using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using System.Net.Sockets;

namespace EL_Agent_Api.Bot.Agents
{
    public class SimpleCopilotAgent
    {
        const string MCSConversationPropertyName = "MCSConversationId";
        private readonly IConfiguration configuration;
        private readonly CopilotClient copilotClient;
        private readonly ITurnContext turnContext;
        private readonly ITurnState turnState;

        public SimpleCopilotAgent(IConfiguration configuration, ITurnContext turnContext, ITurnState turnState, CopilotClient copilotClient)
        {
            this.configuration = configuration;
            this.copilotClient = copilotClient;
            this.turnContext = turnContext;
            this.turnState = turnState;
        }
        
        public async Task InvokeAgentAsync(ChatMessageContent chatMessage, CancellationToken cancellationToken)
        {

            var mcsConversationId = turnState.Conversation.GetValue<string>(MCSConversationPropertyName);
            // Get the CopilotClient
            //var copilotClient = GetClient(this, turnContext);
            // Get or create the conversation
            //var conversationId = string.Empty;
            
            if (string.IsNullOrEmpty(mcsConversationId))
            {
                // Regardless of the Activity  Type, start the conversation.
                await foreach (IActivity activity in copilotClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
                {
                    if (activity.IsType(ActivityTypes.Message))
                    {
                        //turnContext.StreamingResponse.QueueTextChunk(activity.Text);

                        // Record the conversationId MCS is sending. It will be used this for subsequent messages.
                        turnState.Conversation.SetValue(MCSConversationPropertyName, activity.Conversation.Id);

                        await foreach (IActivity activity1 in copilotClient.AskQuestionAsync(turnContext.Activity.Text, mcsConversationId, cancellationToken))
                        {
                            if (activity1.IsType(ActivityTypes.Message))
                            {
                                turnContext.StreamingResponse.QueueTextChunk(activity1.Text);
                            }
                        }
                    }
                }
            }
            else if (turnContext.Activity.IsType(ActivityTypes.Message))
            {
                // Send the Copilot Studio Agent whatever the sent and send the responses back.
                await foreach (IActivity activity in copilotClient.AskQuestionAsync(turnContext.Activity.Text, mcsConversationId, cancellationToken))
                {
                    if (activity.IsType(ActivityTypes.Message))
                    {
                        turnContext.StreamingResponse.QueueTextChunk(activity.Text);
                    }
                }
            }
        }
    }
}
