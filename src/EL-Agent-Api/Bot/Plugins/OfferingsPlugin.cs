using ElAgentApi.Bot.Agents;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;

namespace EL_Agent_Api.Bot.Plugins
{
    [Description("Plugin to program offerings for employees.")]
    public class OfferingsPlugin
    {
        private readonly OfferingsAgent offeringsAgent;
        //private readonly ServiceProvider serviceProvider;

        public OfferingsPlugin(OfferingsAgent offeringsAgent)
        {
            this.offeringsAgent = offeringsAgent;
            //this.serviceProvider = serviceProvider;
        }

        [KernelFunction("get_program_offerings")]
        [Description("Provides offerings based on user query.")]
        public async Task GetOfferings([Description("offerings users request")] string offerings, CancellationToken cancellationToken = default)
        {
            //var turnContext = serviceProvider.GetService<ITurnContext>();
            //var turnState = serviceProvider.GetService<ITurnState>();

            //ChatHistory chatHistory = turnState.GetValue("conversation.chatHistory", () => new ChatHistory());
            await offeringsAgent.InvokeAgentAsync(offerings, cancellationToken);
        }
    }
}
