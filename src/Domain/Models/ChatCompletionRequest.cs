using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RapidCli.Domain.Models;

/// <summary>
/// Describes the payload necessary to request a chat completion.
/// </summary>
public sealed class ChatCompletionRequest
{
    /// <summary>
    /// Gets or sets the model identifier to be used.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of messages that define the conversation context.
    /// </summary>
    [JsonPropertyName("messages")]
    public IList<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

    /// <summary>
    /// Gets or sets a value indicating whether streaming responses are requested.
    /// </summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
        = false;

    /// <summary>
    /// Gets or sets the maximum number of tokens that may be generated.
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the temperature value controlling randomness of the output.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the nucleus sampling parameter.
    /// </summary>
    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the frequency penalty value.
    /// </summary>
    [JsonPropertyName("frequency_penalty")]
    public double? FrequencyPenalty { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the presence penalty value.
    /// </summary>
    [JsonPropertyName("presence_penalty")]
    public double? PresencePenalty { get; set; }
        = null;

    /// <summary>
    /// Gets or sets an optional response format configuration.
    /// </summary>
    [JsonPropertyName("response_format")]
    public ResponseFormat? ResponseFormat { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the optional tool definitions that the assistant can leverage when responding.
    /// </summary>
    [JsonPropertyName("tools")]
    public IList<ToolDefinition>? Tools { get; set; }
        = null;
}
