using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace RapidCli.Domain.Models;

/// <summary>
/// Describes a tool that the assistant can request via tool calls.
/// </summary>
public sealed class ToolDefinition
{
    /// <summary>
    /// Gets or sets the type of the tool. The supported value is <c>function</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    /// <summary>
    /// Gets or sets the function metadata that defines how the tool is invoked.
    /// </summary>
    [JsonPropertyName("function")]
    public ToolFunctionDefinition Function { get; set; } = new();
}

/// <summary>
/// Represents the function contract for a tool definition.
/// </summary>
public sealed class ToolFunctionDefinition
{
    /// <summary>
    /// Gets or sets the name of the tool function.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional human readable description of the tool.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the JSON schema describing the arguments that the tool accepts.
    /// </summary>
    [JsonPropertyName("parameters")]
    public JsonObject Parameters { get; set; } = new();
}
