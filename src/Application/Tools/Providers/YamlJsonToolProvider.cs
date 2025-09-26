using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RapidCli.Domain.Interfaces;
using RapidCli.Domain.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RapidCli.Application.Tools.Providers;

/// <summary>
/// Provides YAML ↔ JSON conversions using an in-process handler.
/// </summary>
public sealed class YamlJsonToolProvider : IAgentToolProvider
{
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlJsonToolProvider"/> class.
    /// </summary>
    public YamlJsonToolProvider()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _serializer = new SerializerBuilder()
            .JsonCompatible()
            .Build();
    }

    /// <inheritdoc />
    public string Name => "builtin.yaml-json";

    /// <inheritdoc />
    public bool CanHandle(McpToolConfiguration configuration)
        => string.Equals(configuration.Execution.Mode, "builtin", StringComparison.OrdinalIgnoreCase)
           && string.Equals(configuration.Execution.Handler, "yaml-json", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<ToolAvailability> GetAvailabilityAsync(McpToolConfiguration configuration, CancellationToken cancellationToken)
        => Task.FromResult(ToolAvailability.Available("Integrado"));

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteAsync(McpToolConfiguration configuration, McpToolInvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var direction = configuration.Metadata.TryGetValue("direction", out var value)
                ? value
                : "yaml-to-json";

            var targetPath = ResolveTargetPath(context);
            var content = await File.ReadAllTextAsync(targetPath, cancellationToken).ConfigureAwait(false);

            string output = direction switch
            {
                "json-to-yaml" => ConvertJsonToYaml(content),
                _ => ConvertYamlToJson(content),
            };

            stopwatch.Stop();
            return ToolExecutionResult.SuccessResult(output, null, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ToolExecutionResult.FailureResult(string.Empty, ex.Message, stopwatch.Elapsed);
        }
    }

    private static string ResolveTargetPath(McpToolInvocationContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.TargetPath) && File.Exists(context.TargetPath))
        {
            return context.TargetPath;
        }

        if (context.Parameters.TryGetValue("target", out var target) && !string.IsNullOrWhiteSpace(target))
        {
            var fullPath = Path.IsPathRooted(target)
                ? target
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), target));

            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        throw new FileNotFoundException("No se pudo determinar el archivo a convertir.");
    }

    private string ConvertYamlToJson(string yaml)
    {
        var yamlObject = _deserializer.Deserialize<object?>(yaml);
        var json = JsonSerializer.Serialize(yamlObject, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        return json;
    }

    private string ConvertJsonToYaml(string json)
    {
        var jsonObject = JsonSerializer.Deserialize<object?>(json);
        var yaml = _serializer.Serialize(jsonObject);
        return yaml;
    }
}
