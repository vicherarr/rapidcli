using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using RapidCli.Domain.Models;

namespace RapidCli.Application.Agents;

/// <summary>
/// Executes tool calls that operate on the local file system inside the configured workspace.
/// </summary>
public sealed class FileSystemToolDispatcher
{
    private readonly string _workspaceRoot;
    private readonly bool _allowWrites;
    private readonly ILogger<FileSystemToolDispatcher> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemToolDispatcher"/> class.
    /// </summary>
    public FileSystemToolDispatcher(string workspaceRoot, bool allowWrites, ILogger<FileSystemToolDispatcher> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        _workspaceRoot = Path.GetFullPath(workspaceRoot);
        Directory.CreateDirectory(_workspaceRoot);
        _allowWrites = allowWrites;
        _logger = logger;

        ToolDefinitions = new List<ToolDefinition>
        {
            CreateListDirectoryDefinition(),
            CreateReadFileDefinition(),
            CreateWriteFileDefinition(),
            CreateAppendFileDefinition(),
        };
    }

    /// <summary>
    /// Gets the collection of tool definitions that the LLM can invoke.
    /// </summary>
    public IReadOnlyList<ToolDefinition> ToolDefinitions { get; }

    /// <summary>
    /// Executes the specified tool call and returns the invocation summary.
    /// </summary>
    public AgentToolInvocation Execute(ChatToolCall toolCall)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        return toolCall.Function.Name switch
        {
            "list_directory" => HandleListDirectory(toolCall.Function.Arguments),
            "read_file" => HandleReadFile(toolCall.Function.Arguments),
            "write_file" => HandleWriteFile(toolCall.Function.Arguments),
            "append_file" => HandleAppendFile(toolCall.Function.Arguments),
            _ => AgentToolInvocation.Failure(toolCall.Function.Name, toolCall.Function.Arguments, $"Tool '{toolCall.Function.Name}' is not supported."),
        };
    }

    private AgentToolInvocation HandleListDirectory(string argumentsJson)
    {
        try
        {
            var args = Deserialize<ListDirectoryArguments>(argumentsJson) ?? new ListDirectoryArguments();
            var target = ResolvePath(args.Path ?? ".");
            if (!Directory.Exists(target))
            {
                return AgentToolInvocation.Failure("list_directory", argumentsJson, $"Directory '{args.Path}' was not found.");
            }

            var directories = Directory
                .EnumerateDirectories(target)
                .Select(ToRelative)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Take(200)
                .ToArray();

            var files = Directory
                .EnumerateFiles(target)
                .Select(ToRelative)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Take(400)
                .ToArray();

            var output = JsonSerializer.Serialize(new
            {
                path = ToRelative(target),
                directories,
                files,
            }, _serializerOptions);

            return AgentToolInvocation.Success("list_directory", argumentsJson, output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list directory with arguments {Arguments}", argumentsJson);
            return AgentToolInvocation.Failure("list_directory", argumentsJson, ex.Message);
        }
    }

    private AgentToolInvocation HandleReadFile(string argumentsJson)
    {
        try
        {
            var args = Deserialize<ReadFileArguments>(argumentsJson);
            if (args is null || string.IsNullOrWhiteSpace(args.Path))
            {
                return AgentToolInvocation.Failure("read_file", argumentsJson, "The 'path' argument is required.");
            }

            var target = ResolvePath(args.Path);
            if (!File.Exists(target))
            {
                return AgentToolInvocation.Failure("read_file", argumentsJson, $"File '{args.Path}' was not found.");
            }

            var content = File.ReadAllText(target);
            if (args.MaxBytes is > 0 && content.Length > args.MaxBytes)
            {
                content = content[..args.MaxBytes.Value];
            }

            var builder = new StringBuilder();
            builder.AppendLine($"FILE: {ToRelative(target)}");
            builder.AppendLine("```text");
            builder.AppendLine(content);
            builder.AppendLine("```");

            return AgentToolInvocation.Success("read_file", argumentsJson, builder.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file with arguments {Arguments}", argumentsJson);
            return AgentToolInvocation.Failure("read_file", argumentsJson, ex.Message);
        }
    }

    private AgentToolInvocation HandleWriteFile(string argumentsJson)
    {
        if (!_allowWrites)
        {
            return AgentToolInvocation.Failure("write_file", argumentsJson, "File write operations are disabled by configuration.");
        }

        try
        {
            var args = Deserialize<WriteFileArguments>(argumentsJson);
            if (args is null || string.IsNullOrWhiteSpace(args.Path))
            {
                return AgentToolInvocation.Failure("write_file", argumentsJson, "The 'path' argument is required.");
            }

            var target = ResolvePath(args.Path);
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(target, args.Content ?? string.Empty);
            var output = $"WROTE_FILE {ToRelative(target)} bytes={args.Content?.Length ?? 0}";
            return AgentToolInvocation.Success("write_file", argumentsJson, output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write file with arguments {Arguments}", argumentsJson);
            return AgentToolInvocation.Failure("write_file", argumentsJson, ex.Message);
        }
    }

    private AgentToolInvocation HandleAppendFile(string argumentsJson)
    {
        if (!_allowWrites)
        {
            return AgentToolInvocation.Failure("append_file", argumentsJson, "File write operations are disabled by configuration.");
        }

        try
        {
            var args = Deserialize<WriteFileArguments>(argumentsJson);
            if (args is null || string.IsNullOrWhiteSpace(args.Path))
            {
                return AgentToolInvocation.Failure("append_file", argumentsJson, "The 'path' argument is required.");
            }

            var target = ResolvePath(args.Path);
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(target, args.Content ?? string.Empty);
            var output = $"APPENDED_FILE {ToRelative(target)} bytes={args.Content?.Length ?? 0}";
            return AgentToolInvocation.Success("append_file", argumentsJson, output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to append file with arguments {Arguments}", argumentsJson);
            return AgentToolInvocation.Failure("append_file", argumentsJson, ex.Message);
        }
    }

    private string ResolvePath(string relativePath)
    {
        var combined = Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(_workspaceRoot, relativePath);
        var fullPath = Path.GetFullPath(combined);

        if (!fullPath.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Attempted to access a path outside of the workspace root.");
        }

        return fullPath;
    }

    private string ToRelative(string fullPath)
    {
        var relative = Path.GetRelativePath(_workspaceRoot, fullPath);
        if (string.IsNullOrWhiteSpace(relative) || relative == ".")
        {
            return ".";
        }

        return relative.Replace('\\', '/');
    }

    private T? Deserialize<T>(string json)
        => string.IsNullOrWhiteSpace(json)
            ? default
            : JsonSerializer.Deserialize<T>(json);

    private static ToolDefinition CreateListDirectoryDefinition()
        => new()
        {
            Function = new ToolFunctionDefinition
            {
                Name = "list_directory",
                Description = "List directories and files relative to the agent workspace.",
                Parameters = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["path"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Relative directory path. Defaults to the workspace root.",
                        },
                    },
                },
            },
        };

    private static ToolDefinition CreateReadFileDefinition()
        => new()
        {
            Function = new ToolFunctionDefinition
            {
                Name = "read_file",
                Description = "Read a UTF-8 text file from the workspace.",
                Parameters = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["path"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Relative file path to open.",
                        },
                        ["max_bytes"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Optional maximum number of characters to return.",
                            ["minimum"] = 1,
                        },
                    },
                    ["required"] = new JsonArray("path"),
                },
            },
        };

    private static ToolDefinition CreateWriteFileDefinition()
        => new()
        {
            Function = new ToolFunctionDefinition
            {
                Name = "write_file",
                Description = "Overwrite a file with the provided UTF-8 content.",
                Parameters = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["path"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Relative file path to write.",
                        },
                        ["content"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Full file contents encoded as UTF-8 text.",
                        },
                    },
                    ["required"] = new JsonArray("path", "content"),
                },
            },
        };

    private static ToolDefinition CreateAppendFileDefinition()
        => new()
        {
            Function = new ToolFunctionDefinition
            {
                Name = "append_file",
                Description = "Append UTF-8 text to a file within the workspace.",
                Parameters = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["path"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Relative file path to append.",
                        },
                        ["content"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Text to append to the file.",
                        },
                    },
                    ["required"] = new JsonArray("path", "content"),
                },
            },
        };

    private sealed record ListDirectoryArguments(string? Path = null);

    private sealed record ReadFileArguments(string Path, int? MaxBytes);

    private sealed record WriteFileArguments(string Path, string? Content);
}
