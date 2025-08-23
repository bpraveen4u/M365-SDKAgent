# Pro code Agent with M365 Agents Toolkit

A modular, extensible toolkit for building intelligent agents and plugins for Microsoft 365 and Teams applications. This project provides a ready-to-use template for conversational bots, message extensions, and embedded web experiences, leveraging .NET Aspire, Semantic Kernel, and Azure OpenAI.

## Features

- **Conversational Agents**: Easily create and orchestrate agents for Teams and Microsoft 365 scenarios.
- **Plugin Architecture**: Extend agent capabilities with plugins for weather, math, date/time, adaptive cards, and more.
- **OpenAI Integration**: Use Azure OpenAI and Semantic Kernel for advanced language and reasoning capabilities.
- **.NET Aspire**: Modern distributed application hosting and service defaults.
- **Authentication**: MSAL-based authentication and user sign-in flows.
- **Health Checks & Telemetry**: Built-in health checks and OpenTelemetry support.

## Project Structure

- `EL-Agent-Api/` — Main API project, agent logic, plugins, and orchestration.
- `EL-AgentAppHost/` — Aspire AppHost for distributed application hosting.
- `EL-AgentServiceDefaults/` — Common service defaults (health, telemetry, discovery).
- `appPackage/` — Teams app manifest and deployment assets.
- `infra/` — Infrastructure-as-code (Bicep) for Azure deployment.

## Key Agents & Plugins

- **LearningAgentSkill**: Main conversational agent, orchestrates plugins and handles user interaction.
- **OrchestratorAgent**: Manages plugin invocation and prompt orchestration.
- **Plugins**:
  - `WeatherForecastPlugin`: Weather info (stubbed, extendable)
  - `OfferingsPlugin`: Program offerings for employees
  - `MathPlugin`: Basic math operations
  - `DateTimePlugin`: Date and time utilities
  - `AdaptiveCardPlugin`: Generates Adaptive Card JSON from data

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [.NET 9 SDK (Preview)](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Azure subscription (for OpenAI integration)

### Build & Run

1. Clone the repository.
2. Configure secrets and environment variables as needed (see `appsettings.json`, `.env.*`).
3. Build and run the distributed app host:dotnet run --project src/EL-AgentAppHost/EL-AgentAppHost.csproj4. The API will be available at the configured endpoint (see launch settings).

### API Endpoint
- `POST /api/messages` — Main endpoint for agent interaction (requires authentication)

## Configuration
- Update `appsettings.json` and environment files for OpenAI, authentication, and other settings.
- See `infra/` for Azure deployment templates.

### Run `dev tunnels`. Please follow [Create and host a dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) and host the tunnel with anonymous user access command as shown below:

   ```bash
   devtunnel host -p 5130 --allow-anonymous
   ```
## Contributing & Support
- File issues or feature requests via [GitHub Issues](https://github.com/OfficeDev/TeamsFx/issues)
- For feedback, use Visual Studio > Help > Send Feedback > Report a Problem

## Resources
- [Microsoft Teams App Documentation](https://aka.ms/Config-Teams-app)
- [Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/overview)

---
This project is provided as a template and reference for building advanced Teams and Microsoft 365 agent solutions.
