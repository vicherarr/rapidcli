using System.Text.Json.Serialization;

namespace RapidCli.Application.Configurations;

/// <summary>
/// Represents configuration options that control how MCP tools are loaded and orchestrated.
/// </summary>
public sealed class ToolingConfiguration
{
    /// <summary>
    /// Gets or sets the relative or absolute path to the tool registry file.
    /// </summary>
    [JsonPropertyName("configuration")]
    public string ConfigurationPath { get; set; } = "agent.tools.yaml";

    /// <summary>
    /// Gets or sets a value indicating whether tools should be executed automáticamente when a match is found.
    /// </summary>
    [JsonPropertyName("auto_execute")]
    public bool AutoExecute { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether availability checks should prefer local installations when possible.
    /// </summary>
    [JsonPropertyName("prefer_local")]
    public bool PreferLocalInstallations { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of characters stored from a tool output.
    /// </summary>
    [JsonPropertyName("max_output_chars")]
    public int MaxOutputCharacters { get; set; } = 8000;
}
