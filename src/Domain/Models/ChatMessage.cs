using System.Collections.Generic;
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

    /// <summary>
    /// Gets or sets the name associated with the message when invoking a tool.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the identifier that links this message to a previous tool call.
    /// </summary>
    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the collection of tool calls emitted by the assistant.
    /// </summary>
    [JsonPropertyName("tool_calls")]
    public IList<ChatToolCall>? ToolCalls { get; set; }
        = null;
}
