using RapidCli.Domain.Interfaces;
using RapidCli.Domain.Models;

namespace RapidCli.Application.Tools;

/// <summary>
/// Represents a tool entry along with the provider that can execute it and its availability state.
/// </summary>
public sealed class McpToolDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolDescriptor"/> class.
    /// </summary>
    public McpToolDescriptor(McpToolConfiguration configuration, IAgentToolProvider? provider, ToolAvailability availability)
    {
        Configuration = configuration;
        Provider = provider;
        Availability = availability;
    }

    /// <summary>
    /// Gets the raw configuration entry.
    /// </summary>
    public McpToolConfiguration Configuration { get; }

    /// <summary>
    /// Gets the provider instance capable of executing the tool.
    /// </summary>
    public IAgentToolProvider? Provider { get; }

    /// <summary>
    /// Gets the availability state.
    /// </summary>
    public ToolAvailability Availability { get; }

    /// <summary>
    /// Gets the display name to render to end users.
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Configuration.DisplayName)
        ? Configuration.Name
        : Configuration.DisplayName!;
}
