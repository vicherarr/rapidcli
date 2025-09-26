using System;
using System.Text.Json.Serialization;

namespace RapidCli.Domain.Models;

/// <summary>
/// Represents the outcome of executing a tool.
/// </summary>
public sealed class ToolExecutionResult
{
    private ToolExecutionResult(bool success, string output, string? error, TimeSpan duration)
    {
        Success = success;
        Output = output;
        Error = error;
        Duration = duration;
    }

    /// <summary>
    /// Gets a value indicating whether the tool finished successfully.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; }

    /// <summary>
    /// Gets the standard output emitted by the tool.
    /// </summary>
    [JsonPropertyName("output")]
    public string Output { get; }

    /// <summary>
    /// Gets the standard error emitted by the tool, if any.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; }

    /// <summary>
    /// Gets the total execution duration.
    /// </summary>
    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; }

    /// <summary>
    /// Creates a successful tool execution result.
    /// </summary>
    public static ToolExecutionResult SuccessResult(string output, string? error, TimeSpan duration)
        => new(true, output, error, duration);

    /// <summary>
    /// Creates a failing tool execution result.
    /// </summary>
    public static ToolExecutionResult FailureResult(string output, string? error, TimeSpan duration)
        => new(false, output, error, duration);
}
