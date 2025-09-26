using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RapidCli.Application.Configurations;
using RapidCli.Domain.Models;

namespace RapidCli.Application.Tools;

/// <summary>
/// Coordinates the selection and execution of MCP tools before delegating to the autonomous agent.
/// </summary>
public sealed class ToolOrchestrator
{
    private readonly McpToolRegistry _registry;
    private readonly McpIntentClassifier _classifier;
    private readonly ToolingConfiguration _configuration;
    private readonly ILogger<ToolOrchestrator> _logger;
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolOrchestrator"/> class.
    /// </summary>
    public ToolOrchestrator(
        McpToolRegistry registry,
        McpIntentClassifier classifier,
        IOptions<ToolingConfiguration> options,
        ILogger<ToolOrchestrator> logger)
    {
        _registry = registry;
        _classifier = classifier;
        _configuration = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Ensures the registry is loaded at least once.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _registry.ReloadAsync(cancellationToken).ConfigureAwait(false);
        _initialized = true;
    }

    /// <summary>
    /// Gets the registered tools along with their availability state.
    /// </summary>
    public IReadOnlyList<McpToolDescriptor> GetRegisteredTools()
        => _registry.Tools;

    /// <summary>
    /// Attempts to orchestrate a tool for the specified objective.
    /// </summary>
    public async Task<ToolOrchestrationResult> TryOrchestrateAsync(string objective, CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!_configuration.AutoExecute)
        {
            return ToolOrchestrationResult.Skip(objective, "La ejecución automática de MCP está deshabilitada.");
        }

        var request = _classifier.Classify(objective);
        var descriptor = _registry.Resolve(request, out var score);
        if (descriptor is null || score <= 0)
        {
            return ToolOrchestrationResult.Skip(objective);
        }

        if (descriptor.Provider is null)
        {
            return ToolOrchestrationResult.Skip(objective, $"No hay proveedor registrado para {descriptor.DisplayName}.");
        }

        if (!descriptor.Availability.IsAvailable)
        {
            var reason = descriptor.Availability.Detail ?? "herramienta no disponible";
            return ToolOrchestrationResult.Skip(objective, $"{descriptor.DisplayName}: {reason}");
        }

        var contextParameters = new Dictionary<string, string>(request.Parameters, StringComparer.OrdinalIgnoreCase)
        {
            ["objective"] = objective,
        };

        var requestedTarget = request.TargetPath;
        if (string.IsNullOrWhiteSpace(requestedTarget)
            && contextParameters.TryGetValue("target", out var existingTarget))
        {
            requestedTarget = existingTarget;
        }

        var resolvedTarget = ResolveTargetPath(requestedTarget);
        if (!string.IsNullOrWhiteSpace(resolvedTarget))
        {
            contextParameters["target"] = resolvedTarget;
        }
        else if (!contextParameters.ContainsKey("target"))
        {
            contextParameters["target"] = Directory.GetCurrentDirectory();
        }

        var context = new McpToolInvocationContext(
            objective,
            resolvedTarget,
            request.Language,
            contextParameters);

        ToolExecutionResult execution;
        try
        {
            execution = await descriptor.Provider
                .ExecuteAsync(descriptor.Configuration, context, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "La herramienta {Tool} falló durante la ejecución", descriptor.DisplayName);
            return ToolOrchestrationResult.Skip(objective, $"{descriptor.DisplayName} falló: {ex.Message}");
        }

        var normalizedOutput = NormalizeOutput(execution.Output);
        execution = execution.Success
            ? ToolExecutionResult.SuccessResult(normalizedOutput, execution.Error, execution.Duration)
            : ToolExecutionResult.FailureResult(normalizedOutput, execution.Error, execution.Duration);

        if (!execution.Success)
        {
            var failure = !string.IsNullOrWhiteSpace(execution.Error)
                ? execution.Error
                : "La herramienta devolvió un código de salida distinto de cero.";
            return ToolOrchestrationResult.Skip(objective, $"{descriptor.DisplayName}: {failure}");
        }

        if (!descriptor.Configuration.ForwardResultToAgent)
        {
            return ToolOrchestrationResult.Complete(objective, descriptor, execution, execution.Output, BuildMessage(descriptor));
        }

        var agentObjective = BuildAgentObjective(objective, descriptor, execution.Output, execution.Error);
        return ToolOrchestrationResult.Forward(objective, agentObjective, descriptor, execution, BuildMessage(descriptor));
    }

    private string? ResolveTargetPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
    }

    private string NormalizeOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return output;
        }

        var trimmed = output.Length > _configuration.MaxOutputCharacters
            ? output[.._configuration.MaxOutputCharacters]
            : output;
        return trimmed.Trim();
    }

    private static string BuildMessage(McpToolDescriptor descriptor)
        => $"Se utilizó la herramienta MCP '{descriptor.DisplayName}'.";

    private static string BuildAgentObjective(string originalObjective, McpToolDescriptor descriptor, string output, string? error)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Actúa como un analista senior.");
        builder.AppendLine("Interpreta los resultados de la herramienta MCP ejecutada automáticamente y responde en español.");
        builder.AppendLine();
        builder.AppendLine($"Solicitud original: {originalObjective}");
        builder.AppendLine();
        builder.AppendLine($"Herramienta: {descriptor.DisplayName} ({descriptor.Configuration.Type})");
        builder.AppendLine("Salida:");
        builder.AppendLine("```text");
        builder.AppendLine(string.IsNullOrWhiteSpace(output) ? "<sin salida>" : output);
        builder.AppendLine("```");

        if (!string.IsNullOrWhiteSpace(error))
        {
            builder.AppendLine();
            builder.AppendLine("Registro de error:");
            builder.AppendLine("```text");
            builder.AppendLine(error!);
            builder.AppendLine("```");
        }

        builder.AppendLine();
        builder.AppendLine("Incluye recomendaciones accionables cuando aplique.");
        return builder.ToString();
    }
}
