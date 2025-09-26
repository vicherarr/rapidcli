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
}
