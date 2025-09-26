using System.Text.Json.Serialization;

namespace RapidCli.Application.Configurations;

/// <summary>
/// Represents the configurable parameters that influence how the assistant interacts with the provider.
/// </summary>
public sealed class ChatConfiguration
{
    /// <summary>
    /// Gets or sets the default model identifier used when talking to the provider.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "zai-org/GLM-4.5-FP8";

    /// <summary>
    /// Gets or sets the default temperature value controlling randomness.
    /// </summary>
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Gets or sets the nucleus sampling parameter.
    /// </summary>
    [JsonPropertyName("top_p")]
    public double TopP { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the maximum number of tokens allowed in a single response.
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the frequency penalty parameter.
    /// </summary>
    [JsonPropertyName("frequency_penalty")]
    public double FrequencyPenalty { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets the presence penalty parameter.
    /// </summary>
    [JsonPropertyName("presence_penalty")]
    public double PresencePenalty { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets a value indicating whether responses should be streamed by default.
    /// </summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = true;

    /// <summary>
    /// Gets or sets the nested configuration that drives the agentic capabilities.
    /// </summary>
    [JsonPropertyName("agent")]
    public AgentConfiguration Agent { get; set; } = new();
}
