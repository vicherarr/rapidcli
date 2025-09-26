using System.Text.Json.Serialization;

namespace RapidCli.Application.Configurations;

/// <summary>
/// Represents the settings that control the agentic capabilities of RapidCLI.
/// </summary>
public sealed class AgentConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the agent functionality is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional model identifier dedicated to agent tasks. When null the chat model is reused.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the maximum number of reasoning iterations before aborting the agent loop.
    /// </summary>
    [JsonPropertyName("max_iterations")]
    public int MaxIterations { get; set; } = 8;

    /// <summary>
    /// Gets or sets a value indicating whether file write operations are allowed.
    /// </summary>
    [JsonPropertyName("allow_file_writes")]
    public bool AllowFileWrites { get; set; } = true;

    /// <summary>
    /// Gets or sets the working directory relative to the current execution folder. Defaults to the current directory.
    /// </summary>
    [JsonPropertyName("working_directory")]
    public string? WorkingDirectory { get; set; }
        = null;
}
