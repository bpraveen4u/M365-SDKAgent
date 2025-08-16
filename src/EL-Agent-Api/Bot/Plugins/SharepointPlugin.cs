using ElAgentApi.Bot.Agents;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;

namespace EL_Agent_Api.Bot.Plugins
{
    [Description("Provides HR policies based on user query.")]
    public class SharepointPlugin
    {
        private readonly SharepointAgent sharepointAgent;
        //private readonly ServiceProvider serviceProvider;

        public SharepointPlugin(SharepointAgent sharepointAgent)
        {
            this.sharepointAgent = sharepointAgent;
            //this.serviceProvider = serviceProvider;
        }

        [KernelFunction("get_hr_policies")]
        [Description("Provides HR policies based on user query.")]
        public async Task GetHRPolicies([Description("HR policies users request")] string hrPolicies, CancellationToken cancellationToken = default)
        {
            //var turnContext = serviceProvider.GetService<ITurnContext>();
            //var turnState = serviceProvider.GetService<ITurnState>();

            //ChatHistory chatHistory = turnState.GetValue("conversation.chatHistory", () => new ChatHistory());
            await sharepointAgent.InvokeAgentAsync(new ChatMessageContent(AuthorRole.User, hrPolicies), cancellationToken);
        }
    }
}
