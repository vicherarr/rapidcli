using System.Text.Json.Serialization;

namespace RapidCli.Domain.Models;

/// <summary>
/// Represents the structure that can be provided to request structured responses.
/// </summary>
public sealed class ResponseFormat
{
    /// <summary>
    /// Gets or sets the response format type (e.g. text, json_object, json_schema).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    /// <summary>
    /// Gets or sets an optional JSON schema used when <see cref="Type"/> equals <c>json_schema</c>.
    /// </summary>
    [JsonPropertyName("json_schema")]
    public object? JsonSchema { get; set; }
        = null;
}
