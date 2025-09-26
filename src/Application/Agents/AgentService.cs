using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RapidCli.Application.Configurations;
using RapidCli.Application.Services;
using RapidCli.Domain.Interfaces;
using RapidCli.Domain.Models;

namespace RapidCli.Application.Agents;

/// <summary>
/// Coordinates agentic interactions that allow the assistant to inspect and modify the local workspace.
/// </summary>
public sealed class AgentService
{
    private readonly IAIClient _client;
    private readonly ConfigurationService _configurationService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AgentService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentService"/> class.
    /// </summary>
    public AgentService(
        IAIClient client,
        ConfigurationService configurationService,
        ILoggerFactory loggerFactory,
        ILogger<AgentService> logger)
    {
        _client = client;
        _configurationService = configurationService;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Executes the agent loop for the provided objective.
    /// </summary>
    /// <param name="objective">The high level goal that the agent should accomplish.</param>
    /// <param name="cancellationToken">Token used to cancel the execution.</param>
    public async Task<AgentExecutionResult> ExecuteTaskAsync(string objective, CancellationToken cancellationToken)
    {
        var configuration = _configurationService.Current;
        if (!configuration.Agent.Enabled)
        {
            return AgentExecutionResult.Failure("El agente está deshabilitado en la configuración actual.", Array.Empty<AgentToolInvocation>());
        }

        var workspaceRoot = ResolveWorkspace(configuration.Agent.WorkingDirectory);
        var dispatcher = new FileSystemToolDispatcher(
            workspaceRoot,
            configuration.Agent.AllowFileWrites,
            _loggerFactory.CreateLogger<FileSystemToolDispatcher>());

        var conversation = new List<ChatMessage>
        {
            new()
            {
                Role = "system",
                Content = BuildSystemPrompt(workspaceRoot),
            },
            new()
            {
                Role = "user",
                Content = objective,
            },
        };

        var toolInvocations = new List<AgentToolInvocation>();
        var maxIterations = Math.Max(1, configuration.Agent.MaxIterations);

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = BuildRequest(conversation, configuration, dispatcher.ToolDefinitions);
            ChatCompletionResponse response;

            try
            {
                response = await _client.CreateChatCompletionAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent invocation failed on iteration {Iteration}", iteration + 1);
                return AgentExecutionResult.Failure($"No se pudo contactar al modelo: {ex.Message}", toolInvocations);
            }

            var assistantMessage = response.Choices.FirstOrDefault()?.Message;
            if (assistantMessage is null)
            {
                continue;
            }

            if (assistantMessage.ToolCalls is { Count: > 0 })
            {
                conversation.Add(assistantMessage);

                foreach (var toolCall in assistantMessage.ToolCalls)
                {
                    var invocation = dispatcher.Execute(toolCall);
                    toolInvocations.Add(invocation);

                    conversation.Add(new ChatMessage
                    {
                        Role = "tool",
                        ToolCallId = toolCall.Id,
                        Content = invocation.Output,
                    });

                    if (invocation.IsError)
                    {
                        _logger.LogWarning(
                            "Tool call {Tool} returned an error: {Message}",
                            toolCall.Function.Name,
                            invocation.Output);
                    }
                }

                continue;
            }

            if (!string.IsNullOrWhiteSpace(assistantMessage.Content))
            {
                conversation.Add(assistantMessage);
                return AgentExecutionResult.Success(assistantMessage.Content, toolInvocations);
            }

            conversation.Add(assistantMessage);
        }

        return AgentExecutionResult.Failure(
            "El agente alcanzó el número máximo de iteraciones sin producir un resultado final.",
            toolInvocations);
    }

    private static ChatCompletionRequest BuildRequest(
        IList<ChatMessage> messages,
        ChatConfiguration configuration,
        IReadOnlyList<ToolDefinition> tools)
    {
        var request = new ChatCompletionRequest
        {
            Model = !string.IsNullOrWhiteSpace(configuration.Agent.Model)
                ? configuration.Agent.Model!
                : configuration.Model,
            Stream = false,
            Temperature = configuration.Temperature,
            TopP = configuration.TopP,
            MaxTokens = configuration.MaxTokens,
            FrequencyPenalty = configuration.FrequencyPenalty,
            PresencePenalty = configuration.PresencePenalty,
            Messages = messages.ToList(),
            Tools = tools.ToList(),
        };

        return request;
    }

    private static string BuildSystemPrompt(string workspaceRoot)
    {
        var normalizedRoot = workspaceRoot.Replace('\\', '/');
        return $"Eres RapidCLI, un agente de desarrollo con herramientas para explorar y modificar archivos dentro de '{normalizedRoot}'. " +
               "Antes de proponer cambios, inspecciona el código relevante usando las herramientas disponibles (list_directory, read_file). " +
               "Solo modifica archivos cuando sea necesario y explica claramente cada cambio que realices. " +
               "Tu respuesta final debe incluir un resumen conciso, los archivos modificados y los siguientes pasos recomendados.";
    }

    private static string ResolveWorkspace(string? configuredPath)
    {
        var basePath = Directory.GetCurrentDirectory();
        if (string.IsNullOrWhiteSpace(configuredPath) || configuredPath == ".")
        {
            return basePath;
        }

        var candidate = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(basePath, configuredPath);

        return Path.GetFullPath(candidate);
    }
}
