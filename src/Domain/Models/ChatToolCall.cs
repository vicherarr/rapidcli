using System.Text.Json.Serialization;

namespace RapidCli.Domain.Models;

/// <summary>
/// Represents a tool invocation emitted by the assistant.
/// </summary>
public sealed class ChatToolCall
{
    /// <summary>
    /// Gets or sets the identifier of the tool call.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of tool call. Currently only "function" is supported.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    /// <summary>
    /// Gets or sets the function payload that describes the tool to execute.
    /// </summary>
    [JsonPropertyName("function")]
    public ChatToolCallFunction Function { get; set; } = new();
}

/// <summary>
/// Represents the details of a tool function call.
/// </summary>
public sealed class ChatToolCallFunction
{
    /// <summary>
    /// Gets or sets the function name to be executed.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stringified JSON arguments passed by the assistant.
    /// </summary>
    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;
}
