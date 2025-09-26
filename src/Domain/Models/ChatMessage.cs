using System.Text.Json.Serialization;

namespace RapidCli.Domain.Models;

/// <summary>
/// Represents a single message in a chat style conversation.
/// </summary>
public sealed class ChatMessage
{
    /// <summary>
    /// Gets or sets the role of the author of the message (e.g. system, user, assistant).
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the textual content of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
