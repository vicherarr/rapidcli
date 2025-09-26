using System;
using System.Text.Json.Serialization;

namespace RapidCli.Domain.Models;

/// <summary>
/// Represents a lightweight projection of a stored conversation session.
/// </summary>
public sealed class ConversationSessionSummary
{
    /// <summary>
    /// Gets or sets the identifier of the session.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the session was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp of the last update made to the session.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the number of messages stored in the session.
    /// </summary>
    [JsonPropertyName("messageCount")]
    public int MessageCount { get; set; }
        = 0;

    /// <summary>
    /// Gets or sets the number of tools registered as used within the session.
    /// </summary>
    [JsonPropertyName("toolCount")]
    public int ToolCount { get; set; }
        = 0;
}
