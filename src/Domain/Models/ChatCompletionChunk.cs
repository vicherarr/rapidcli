using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RapidCli.Domain.Models;

/// <summary>
/// Represents an incremental piece of the streamed chat completion response.
/// </summary>
public sealed class ChatCompletionChunk
{
    /// <summary>
    /// Gets or sets the list of incremental choices.
    /// </summary>
    [JsonPropertyName("choices")]
    public IList<ChatCompletionChunkChoice> Choices { get; set; } = new List<ChatCompletionChunkChoice>();
}

/// <summary>
/// Represents the incremental delta information for a streamed choice.
/// </summary>
public sealed class ChatCompletionChunkChoice
{
    /// <summary>
    /// Gets or sets the delta message containing new text for the streamed completion.
    /// </summary>
    [JsonPropertyName("delta")]
    public ChatMessage? Delta { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the reason that the streamed completion finished, if applicable.
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the incremental tool call payload emitted while streaming.
    /// </summary>
    [JsonPropertyName("tool_calls")]
    public IList<ChatToolCallDelta>? ToolCalls { get; set; }
        = null;
}

/// <summary>
/// Represents the delta information for a streaming tool call payload.
/// </summary>
public sealed class ChatToolCallDelta
{
    /// <summary>
    /// Gets or sets the index of the tool call in the response.
    /// </summary>
    [JsonPropertyName("index")]
    public int? Index { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the identifier for the tool call.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the type associated with the delta payload.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the function details of the tool call delta.
    /// </summary>
    [JsonPropertyName("function")]
    public ChatToolCallFunctionDelta? Function { get; set; }
        = null;
}

/// <summary>
/// Represents the function specific delta for a streaming tool call.
/// </summary>
public sealed class ChatToolCallFunctionDelta
{
    /// <summary>
    /// Gets or sets the function name provided during streaming.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the partial arguments payload for the tool call.
    /// </summary>
    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
        = null;
}
