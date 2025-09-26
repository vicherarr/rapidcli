using System;
using System.Collections.Generic;
using System.Linq;

namespace RapidCli.Application.Tools;

/// <summary>
/// Represents the normalized information extracted from the user intent.
/// </summary>
public sealed class McpToolRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolRequest"/> class.
    /// </summary>
    public McpToolRequest(
        string originalObjective,
        string? task,
        string? language,
        string? fileExtension,
        string? targetPath,
        IReadOnlyCollection<string> keywords,
        IReadOnlyDictionary<string, string> parameters)
    {
        OriginalObjective = originalObjective;
        Task = task;
        Language = language;
        FileExtension = fileExtension;
        TargetPath = targetPath;
        Keywords = keywords;
        Parameters = parameters;
    }

    /// <summary>
    /// Gets the original user objective.
    /// </summary>
    public string OriginalObjective { get; }

    /// <summary>
    /// Gets the classified task type such as security or linting.
    /// </summary>
    public string? Task { get; }

    /// <summary>
    /// Gets the detected programming language.
    /// </summary>
    public string? Language { get; }

    /// <summary>
    /// Gets the primary file extension referenced in the request.
    /// </summary>
    public string? FileExtension { get; }

    /// <summary>
    /// Gets the path that appears to be the main subject of the request.
    /// </summary>
    public string? TargetPath { get; }

    /// <summary>
    /// Gets the set of keywords that influenced the classification.
    /// </summary>
    public IReadOnlyCollection<string> Keywords { get; }

    /// <summary>
    /// Gets the extracted parameters.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>
    /// Determines whether the specified keyword was found during classification.
    /// </summary>
    public bool ContainsKeyword(string keyword)
        => Keywords.Contains(keyword, StringComparer.OrdinalIgnoreCase);
}
