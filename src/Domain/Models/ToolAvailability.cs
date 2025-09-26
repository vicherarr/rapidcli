using System.Text.Json.Serialization;

namespace RapidCli.Domain.Models;

/// <summary>
/// Represents the availability status of an MCP tool.
/// </summary>
public sealed class ToolAvailability
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolAvailability"/> class.
    /// </summary>
    /// <param name="isAvailable">Whether the tool can be executed.</param>
    /// <param name="detail">Optional descriptive detail.</param>
    [JsonConstructor]
    public ToolAvailability(bool isAvailable, string? detail = null)
    {
        IsAvailable = isAvailable;
        Detail = detail;
    }

    /// <summary>
    /// Gets a value indicating whether the tool can be executed.
    /// </summary>
    [JsonPropertyName("available")]
    public bool IsAvailable { get; }

    /// <summary>
    /// Gets a human readable description of the availability state.
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; }

    /// <summary>
    /// Creates an availability instance marked as available.
    /// </summary>
    public static ToolAvailability Available(string? detail = null)
        => new(true, detail);

    /// <summary>
    /// Creates an availability instance marked as unavailable.
    /// </summary>
    public static ToolAvailability Unavailable(string? detail = null)
        => new(false, detail);
}
