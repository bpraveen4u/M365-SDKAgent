using Microsoft.Agents.CopilotStudio.Client;

namespace MyM365Agent1
{
    //public class ConfigOptions
    //{
    //    public AzureConfigOptions Azure { get; set; }
    //}

    ///// <summary>
    ///// Options for Azure OpenAI and Azure Content Safety
    ///// </summary>
    //public class AzureConfigOptions
    //{
    //    public string OpenAIApiKey { get; set; }
    //    public string OpenAIEndpoint { get; set; }
    //    public string OpenAIDeploymentName { get; set; }
    //}

    internal class SampleConnectionSettings : ConnectionSettings
    {
        /// <summary>
        /// Use S2S connection for authentication.
        /// </summary>
        public bool UseS2SConnection { get; set; } = false;

        /// <summary>
        /// Tenant ID for creating the authentication for the connection
        /// </summary>
        public string? TenantId { get; set; }
        /// <summary>
        /// Application ID for creating the authentication for the connection
        /// </summary>
        public string? AppClientId { get; set; }

        /// <summary>
        /// Application secret for creating the authentication for the connection
        /// </summary>
        public string? AppClientSecret { get; set; }

        /// <summary>
        /// Create ConnectionSettings from a configuration section.
        /// </summary>
        /// <param name="config"></param>
        /// <exception cref="ArgumentException"></exception>
        public SampleConnectionSettings(IConfigurationSection config) : base(config)
        {
            AppClientId = config[nameof(AppClientId)] ?? throw new ArgumentException($"{nameof(AppClientId)} not found in config");
            TenantId = config[nameof(TenantId)] ?? throw new ArgumentException($"{nameof(TenantId)} not found in config");

            UseS2SConnection = config.GetValue<bool>(nameof(UseS2SConnection), false);
            AppClientSecret = config[nameof(AppClientSecret)];

        }
    }
}