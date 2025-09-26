using System.Collections.Generic;

namespace RapidCli.Domain.Models;

/// <summary>
/// Provides contextual information for tool execution based on the user request.
/// </summary>
public sealed class McpToolInvocationContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolInvocationContext"/> class.
    /// </summary>
    public McpToolInvocationContext(
        string objective,
        string? targetPath,
        string? language,
        IReadOnlyDictionary<string, string> parameters)
    {
        Objective = objective;
        TargetPath = targetPath;
        Language = language;
        Parameters = parameters;
    }

    /// <summary>
    /// Gets the normalized user objective that originated the invocation.
    /// </summary>
    public string Objective { get; }

    /// <summary>
    /// Gets the primary file or resource path associated with the request, when available.
    /// </summary>
    public string? TargetPath { get; }

    /// <summary>
    /// Gets the language that best matches the request context.
    /// </summary>
    public string? Language { get; }

    /// <summary>
    /// Gets additional parameters extracted from the intent such as file extensions or auxiliary flags.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }
}
