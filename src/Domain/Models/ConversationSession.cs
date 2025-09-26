using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RapidCli.Domain.Models;

/// <summary>
/// Represents a persisted conversation session for the CLI assistant.
/// </summary>
public sealed class ConversationSession
{
    /// <summary>
    /// Gets or sets the identifier associated with the session.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the session was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the session was last updated.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the collection of messages exchanged during the session.
    /// </summary>
    [JsonPropertyName("messages")]
    public IList<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

    /// <summary>
    /// Gets or sets the agent state associated with the session.
    /// </summary>
    [JsonPropertyName("agentState")]
    public AgentState AgentState { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of tool identifiers used during the session.
    /// </summary>
    [JsonPropertyName("toolsUsed")]
    public IList<string> ToolsUsed { get; set; } = new List<string>();
}
