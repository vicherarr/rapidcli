using System.Text.Json.Serialization;

namespace RapidCli.Domain.Models;

/// <summary>
/// Represents a full chat completion response as returned by the provider when streaming is disabled.
/// </summary>
public sealed class ChatCompletionResponse
{
    /// <summary>
    /// Gets or sets the unique identifier of the completion request.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the object type returned by the provider.
    /// </summary>
    [JsonPropertyName("object")]
    public string? Object { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the collection of choices returned by the provider.
    /// </summary>
    [JsonPropertyName("choices")]
    public IList<ChatCompletionChoice> Choices { get; set; } = new List<ChatCompletionChoice>();
}

/// <summary>
/// Represents a single choice within a chat completion response.
/// </summary>
public sealed class ChatCompletionChoice
{
    /// <summary>
    /// Gets or sets the index of the choice.
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }
        = 0;

    /// <summary>
    /// Gets or sets the generated message.
    /// </summary>
    [JsonPropertyName("message")]
    public ChatMessage Message { get; set; } = new();
}
