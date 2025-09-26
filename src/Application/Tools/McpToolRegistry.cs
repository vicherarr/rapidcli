using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RapidCli.Application.Configurations;
using RapidCli.Domain.Interfaces;
using RapidCli.Domain.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RapidCli.Application.Tools;

/// <summary>
/// Loads and keeps track of the MCP tools defined in the configuration registry.
/// </summary>
public sealed class McpToolRegistry
{
    private readonly IEnumerable<IAgentToolProvider> _providers;
    private readonly ToolingConfiguration _configuration;
    private readonly ILogger<McpToolRegistry> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private IReadOnlyList<McpToolDescriptor> _tools = Array.Empty<McpToolDescriptor>();
    private readonly IDeserializer _deserializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolRegistry"/> class.
    /// </summary>
    public McpToolRegistry(IEnumerable<IAgentToolProvider> providers, IOptions<ToolingConfiguration> options, ILogger<McpToolRegistry> logger)
    {
        _providers = providers;
        _configuration = options.Value;
        _logger = logger;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Gets the list of tools available in the registry.
    /// </summary>
    public IReadOnlyList<McpToolDescriptor> Tools => _tools;

    /// <summary>
    /// Reloads the registry from disk, refreshing availability information.
    /// </summary>
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = ResolveRegistryPath();
            if (!File.Exists(path))
            {
                _logger.LogWarning("El archivo de herramientas MCP '{File}' no existe.", path);
                _tools = Array.Empty<McpToolDescriptor>();
                return;
            }

            var yaml = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var document = _deserializer.Deserialize<McpToolRegistryDocument>(yaml) ?? new McpToolRegistryDocument();
            var descriptors = new List<McpToolDescriptor>();

            foreach (var tool in document.Tools)
            {
                if (!tool.Enabled)
                {
                    descriptors.Add(new McpToolDescriptor(tool, null, ToolAvailability.Unavailable("Deshabilitado")));
                    continue;
                }

                var provider = _providers.FirstOrDefault(p => p.CanHandle(tool));
                if (provider is null)
                {
                    descriptors.Add(new McpToolDescriptor(tool, null, ToolAvailability.Unavailable("Sin proveedor")));
                    continue;
                }

                ToolAvailability availability;
                try
                {
                    availability = await provider.GetAvailabilityAsync(tool, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo verificar la disponibilidad de {Tool}", tool.Name);
                    availability = ToolAvailability.Unavailable(ex.Message);
                }

                descriptors.Add(new McpToolDescriptor(tool, provider, availability));
            }

            _tools = descriptors;
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>
    /// Attempts to resolve the best tool descriptor for the supplied request.
    /// </summary>
    public McpToolDescriptor? Resolve(McpToolRequest request, out int score)
    {
        score = 0;
        if (_tools.Count == 0)
        {
            return null;
        }

        McpToolDescriptor? best = null;
        var bestScore = 0;

        foreach (var descriptor in _tools)
        {
            var currentScore = Score(descriptor.Configuration, request);
            if (currentScore <= 0)
            {
                continue;
            }

            if (!descriptor.Availability.IsAvailable)
            {
                continue;
            }

            if (best is null
                || currentScore > bestScore
                || (currentScore == bestScore && ComparePriority(descriptor.Configuration, best.Configuration) < 0))
            {
                best = descriptor;
                bestScore = currentScore;
            }
        }

        score = bestScore;
        return best;
    }

    private static int ComparePriority(McpToolConfiguration left, McpToolConfiguration right)
    {
        var leftPriority = left.Priority ?? int.MaxValue;
        var rightPriority = right.Priority ?? int.MaxValue;
        return leftPriority.CompareTo(rightPriority);
    }

    private static int Score(McpToolConfiguration configuration, McpToolRequest request)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(request.Task)
            && configuration.Tasks.Any(task => string.Equals(task, request.Task, StringComparison.OrdinalIgnoreCase)))
        {
            score += 6;
        }

        if (!string.IsNullOrWhiteSpace(request.Language)
            && configuration.Languages.Any(lang => string.Equals(lang, request.Language, StringComparison.OrdinalIgnoreCase)))
        {
            score += 3;
        }

        if (!string.IsNullOrWhiteSpace(request.FileExtension)
            && configuration.FileExtensions.Any(ext => string.Equals(ext.TrimStart('.'), request.FileExtension.TrimStart('.'), StringComparison.OrdinalIgnoreCase)))
        {
            score += 2;
        }

        if (configuration.IntentKeywords.Count > 0)
        {
            var matches = configuration.IntentKeywords.Count(keyword => request.ContainsKeyword(keyword));
            score += matches;
        }

        if (configuration.Tasks.Any(task => string.Equals(task, "conversion", StringComparison.OrdinalIgnoreCase))
            && request.Task is null
            && request.FileExtension is not null)
        {
            score += 2;
        }

        return score;
    }

    private string ResolveRegistryPath()
    {
        var candidate = _configuration.ConfigurationPath;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = "agent.tools.yaml";
        }

        if (Path.IsPathRooted(candidate))
        {
            return candidate;
        }

        var basePath = Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(basePath, candidate));
    }
}
