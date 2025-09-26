using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RapidCli.Domain.Models;

/// <summary>
/// Represents a Model Context Protocol tool entry loaded from the registry configuration file.
/// </summary>
public sealed class McpToolConfiguration
{
    /// <summary>
    /// Gets or sets the internal identifier of the tool.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user facing display name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the general tool type (for example security, linting, docs, etc.).
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }
        = null;

    /// <summary>
    /// Gets or sets a value indicating whether the tool is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the optional priority used to break ties when resolving tools.
    /// Lower values take precedence.
    /// </summary>
    [JsonPropertyName("priority")]
    public int? Priority { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the execution metadata that describes how the tool should be invoked.
    /// </summary>
    [JsonPropertyName("execution")]
    public McpToolExecution Execution { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of high level tasks supported by the tool (security, linting, conversion, etc.).
    /// </summary>
    [JsonPropertyName("tasks")]
    public List<string> Tasks { get; set; } = new();

    /// <summary>
    /// Gets or sets the languages or ecosystems supported by the tool.
    /// </summary>
    [JsonPropertyName("languages")]
    public List<string> Languages { get; set; } = new();

    /// <summary>
    /// Gets or sets the file extensions that the tool understands.
    /// </summary>
    [JsonPropertyName("file_extensions")]
    public List<string> FileExtensions { get; set; } = new();

    /// <summary>
    /// Gets or sets the keywords that should trigger the tool when found in the user intent.
    /// </summary>
    [JsonPropertyName("intent_keywords")]
    public List<string> IntentKeywords { get; set; } = new();

    /// <summary>
    /// Gets or sets arbitrary metadata passed to the provider.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets an optional description of the capabilities offered by the tool.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
        = null;

    /// <summary>
    /// Gets or sets a value indicating whether the raw tool output must be interpreted by the agent afterwards.
    /// </summary>
    [JsonPropertyName("forward_result_to_agent")]
    public bool ForwardResultToAgent { get; set; } = true;
}

/// <summary>
/// Encapsulates the execution contract for a tool entry.
/// </summary>
public sealed class McpToolExecution
{
    /// <summary>
    /// Gets or sets the execution mode (cli, http, builtin, socket, etc.).
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "cli";

    /// <summary>
    /// Gets or sets the command or endpoint associated with the tool.
    /// </summary>
    [JsonPropertyName("command")]
    public string? Command { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the command arguments.
    /// </summary>
    [JsonPropertyName("arguments")]
    public List<string> Arguments { get; set; } = new();

    /// <summary>
    /// Gets or sets an optional handler identifier used by builtin providers.
    /// </summary>
    [JsonPropertyName("handler")]
    public string? Handler { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the working directory used when running the tool.
    /// </summary>
    [JsonPropertyName("working_directory")]
    public string? WorkingDirectory { get; set; }
        = null;

    /// <summary>
    /// Gets or sets optional environment variables that should be present when executing the tool.
    /// </summary>
    [JsonPropertyName("environment")]
    public Dictionary<string, string> Environment { get; set; } = new();
}

/// <summary>
/// Represents a typed model for the tool registry file.
/// </summary>
public sealed class McpToolRegistryDocument
{
    /// <summary>
    /// Gets or sets the collection of tools defined in the registry.
    /// </summary>
    [JsonPropertyName("tools")]
    public List<McpToolConfiguration> Tools { get; set; } = new();
}
