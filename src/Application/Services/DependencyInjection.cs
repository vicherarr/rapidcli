using Microsoft.Extensions.DependencyInjection;
using RapidCli.Application.Agents;
using RapidCli.Application.Conversation;
using RapidCli.Application.Sessions;
using RapidCli.Application.Tools;
using RapidCli.Application.Tools.Providers;
using RapidCli.Domain.Interfaces;

namespace RapidCli.Application.Services;

/// <summary>
/// Provides dependency injection helpers for the application layer.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the application layer services to the service collection.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<ConversationManager>();
        services.AddSingleton<SessionStorageService>();
        services.AddSingleton<ConfigurationService>();
        services.AddSingleton<ChatService>();
        services.AddSingleton<AgentService>();
        services.AddSingleton<McpIntentClassifier>();
        services.AddSingleton<McpToolRegistry>();
        services.AddSingleton<ToolOrchestrator>();
        services.AddSingleton<IAgentToolProvider, CliToolProvider>();
        services.AddSingleton<IAgentToolProvider, YamlJsonToolProvider>();
        return services;
    }
}
