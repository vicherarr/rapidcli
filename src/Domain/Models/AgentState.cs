using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RapidCli.Domain.Models;

/// <summary>
/// Represents the internal state of the autonomous agent for a conversation session.
/// </summary>
public sealed class AgentState
{
    /// <summary>
    /// Gets or sets the collection of file paths that have been read or written during the session.
    /// </summary>
    [JsonPropertyName("loadedFiles")]
    public IList<string> LoadedFiles { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the collection of tool identifiers used during the session.
    /// </summary>
    [JsonPropertyName("activeTools")]
    public IList<string> ActiveTools { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets a dictionary with the configuration snapshot that was active for the agent.
    /// </summary>
    [JsonPropertyName("configurationSnapshot")]
    public IDictionary<string, string> ConfigurationSnapshot { get; set; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets a collection capturing the internal reasoning traces shared with the user.
    /// </summary>
    [JsonPropertyName("thoughtLog")]
    public IList<string> ThoughtLog { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the most recent summary generated for the conversation history.
    /// </summary>
    [JsonPropertyName("lastSummary")]
    public string? LastSummary { get; set; }
        = null;

    /// <summary>
    /// Gets or sets the timestamp when the state was last updated.
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}
