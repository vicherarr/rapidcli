using System.Threading;
using System.Threading.Tasks;
using RapidCli.Domain.Models;

namespace RapidCli.Domain.Interfaces;

/// <summary>
/// Provides the contract required to execute external Model Context Protocol tools.
/// </summary>
public interface IAgentToolProvider
{
    /// <summary>
    /// Gets the unique name of the provider implementation.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Determines whether the provider can handle the supplied tool configuration.
    /// </summary>
    /// <param name="configuration">The tool configuration to evaluate.</param>
    /// <returns><c>true</c> when the provider can execute the tool; otherwise, <c>false</c>.</returns>
    bool CanHandle(McpToolConfiguration configuration);

    /// <summary>
    /// Computes the availability state for the specified tool.
    /// </summary>
    /// <param name="configuration">The tool configuration.</param>
    /// <param name="cancellationToken">Token used to cancel the check.</param>
    Task<ToolAvailability> GetAvailabilityAsync(McpToolConfiguration configuration, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the tool using the provided context.
    /// </summary>
    /// <param name="configuration">The tool configuration.</param>
    /// <param name="context">Invocation context extracted from the user intent.</param>
    /// <param name="cancellationToken">Token used to cancel the execution.</param>
    Task<ToolExecutionResult> ExecuteAsync(McpToolConfiguration configuration, McpToolInvocationContext context, CancellationToken cancellationToken);
}
