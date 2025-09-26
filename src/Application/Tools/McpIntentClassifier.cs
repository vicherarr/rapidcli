using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RapidCli.Application.Tools;

/// <summary>
/// Performs lightweight semantic analysis on top of the user input to decide which MCP tool is relevant.
/// </summary>
public sealed class McpIntentClassifier
{
    private static readonly Regex FilePattern = new(@"(?<path>[^\s]+\.(cs|fs|vb|py|rb|js|ts|tsx|jsx|java|kt|kts|go|rs|php|json|ya?ml|md|xml|gradle|sln|csproj))", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LanguagePattern = new(@"\b(c#|csharp|dotnet|f#|javascript|typescript|python|java|kotlin|go|rust|php|ruby|kotlin)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly char[] PathTokenSeparators = { ' ', '\n', '\r', '\t' };
    private static readonly char[] PathSurroundingPunctuation = { '"', '\'', '`', '(', ')', '[', ']', '{', '}', '<', '>' };
    private static readonly char[] PathTrailingPunctuation = { ',', ';', ':', '!', '?' };

    private static readonly (string Task, string[] Keywords)[] TaskMappings =
    [
        ("security", new[] { "security", "seguridad", "vulnerability", "vulnerabilidad", "sast", "semgrep", "owasp", "auditar" }),
        ("secret-scanning", new[] { "secret", "secreto", "credencial", "gitleaks", "api key" }),
        ("lint", new[] { "lint", "linter", "formatea", "format", "dotnet-format", "style", "analizador" }),
        ("testing", new[] { "test", "tests", "pruebas", "coverage", "unitarias" }),
        ("documentation", new[] { "docfx", "documenta", "documentación", "dokka", "docs" }),
        ("dependency", new[] { "dependencia", "dependencies", "árbol", "tree", "sbom" }),
        ("analysis", new[] { "análisis", "analysis", "analiza", "static" }),
        ("conversion", new[] { "convierte", "convert", "transforma", "traduce", "yq", "yaml", "json", "xml" }),
        ("logs", new[] { "log", "logs", "registro", "traza" }),
    ];

    /// <summary>
    /// Creates an <see cref="McpToolRequest"/> using keywords and heuristics.
    /// </summary>
    /// <param name="objective">The user objective.</param>
    public McpToolRequest Classify(string objective)
    {
        if (string.IsNullOrWhiteSpace(objective))
        {
            throw new ArgumentException("Objective cannot be empty.", nameof(objective));
        }

        var lowered = objective.ToLowerInvariant();
        var keywords = ExtractKeywords(lowered);
        var task = DetermineTask(keywords, lowered);
        var targetPath = ExtractPath(objective);
        var language = ExtractLanguage(lowered, targetPath);
        var extension = ExtractExtension(targetPath);
        var parameters = BuildParameters(targetPath, extension, language);

        return new McpToolRequest(objective, task, language, extension, targetPath, keywords, parameters);
    }

    private static IReadOnlyCollection<string> ExtractKeywords(string normalized)
    {
        var delimiters = new[] { ' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']' };
        var words = normalized.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
        return new HashSet<string>(words, StringComparer.OrdinalIgnoreCase);
    }

    private static string? DetermineTask(IEnumerable<string> keywords, string normalized)
    {
        foreach (var (task, signals) in TaskMappings)
        {
            if (signals.Any(signal => keywords.Contains(signal, StringComparer.OrdinalIgnoreCase)))
            {
                return task;
            }
        }

        if (normalized.Contains("scan", StringComparison.OrdinalIgnoreCase))
        {
            return "analysis";
        }

        return null;
    }

    private static string? ExtractPath(string objective)
    {
        var match = FilePattern.Match(objective);
        if (match.Success)
        {
            return match.Groups["path"].Value;
        }

        foreach (var candidate in EnumeratePathCandidates(objective))
        {
            if (LooksLikePath(candidate))
            {
                return candidate;
            }

            if (ExistsRelativePath(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumeratePathCandidates(string objective)
    {
        var tokens = objective.Split(PathTokenSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var trimmed = token
                .Trim(PathSurroundingPunctuation)
                .TrimEnd(PathTrailingPunctuation);

            if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.All(c => c == '.'))
            {
                trimmed = trimmed.TrimEnd('.');
            }

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                yield return trimmed;
            }
        }
    }

    private static bool LooksLikePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        if (value is "." or "..")
        {
            return true;
        }

        if (value.StartsWith("./", StringComparison.Ordinal)
            || value.StartsWith("../", StringComparison.Ordinal)
            || value.StartsWith(".\\", StringComparison.Ordinal)
            || value.StartsWith("..\\", StringComparison.Ordinal))
        {
            return true;
        }

        if (value.Contains('/') || value.Contains('\\'))
        {
            return true;
        }

        return value.Length >= 2 && value[1] == ':' && char.IsLetter(value[0]);
    }

    private static bool ExistsRelativePath(string candidate)
    {
        try
        {
            var combined = Path.Combine(Directory.GetCurrentDirectory(), candidate);
            return File.Exists(combined) || Directory.Exists(combined);
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractLanguage(string normalizedObjective, string? path)
    {
        var languageMatch = LanguagePattern.Match(normalizedObjective);
        if (languageMatch.Success)
        {
            return NormalizeLanguage(languageMatch.Value);
        }

        if (!string.IsNullOrEmpty(path))
        {
            var extension = ExtractExtension(path);
            return extension switch
            {
                ".cs" or ".csproj" or ".sln" => "csharp",
                ".fs" => "fsharp",
                ".vb" => "vbnet",
                ".py" => "python",
                ".js" or ".jsx" => "javascript",
                ".ts" or ".tsx" => "typescript",
                ".java" => "java",
                ".kt" or ".kts" => "kotlin",
                ".go" => "go",
                ".rs" => "rust",
                ".php" => "php",
                ".rb" => "ruby",
                _ => null,
            };
        }

        return null;
    }

    private static string NormalizeLanguage(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "c#" or "csharp" or "dotnet" => "csharp",
            "f#" => "fsharp",
            _ => language.ToLowerInvariant(),
        };
    }

    private static string? ExtractExtension(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var index = path.LastIndexOf('.');
        return index >= 0 ? path[index..].ToLowerInvariant() : null;
    }

    private static IReadOnlyDictionary<string, string> BuildParameters(string? path, string? extension, string? language)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(path))
        {
            result["target"] = path;
        }

        if (!string.IsNullOrWhiteSpace(extension))
        {
            result["extension"] = extension.TrimStart('.');
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            result["language"] = language;
        }

        return result;
    }
}
