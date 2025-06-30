using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

namespace EL_Agent_Api.Bot.Tools
{
    public class PlaywriteMcpClient
    {
        private readonly Kernel _kernel;
        //private readonly IMcpClient mcpClient;

        public PlaywriteMcpClient(Kernel kernel)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            

            //kernel.Plugins.Add(KernelPluginFactory.CreateFromType<PlaywriteMcpClient>(serviceProvider: kernel.Services));
        }

        //writea code to invoke the list of tools form mcp client
        public async Task<IList<McpClientTool>> GetAvailableToolsAsync()
        {
            
            var mcpClient = await McpClientFactory.CreateAsync(new StdioClientTransport(new()
            {
                Name = "MCPServer",
                Command = "npx",
                Arguments = new[] { "-y", "@modelcontextprotocol/server-github" },
            }));
            var tools = await mcpClient.ListToolsAsync();
            if(tools != null)
            {
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                _kernel.Plugins.AddFromFunctions("GitHub", tools.Select(aiFunction => aiFunction.AsKernelFunction()));
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            }
            //Console.WriteLine($"Found {tools.Count} tools available in MCP client.");
            return tools;
        }
    }
}
