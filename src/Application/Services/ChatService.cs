using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RapidCli.Application.Configurations;
using RapidCli.Application.Conversation;
using RapidCli.Application.Sessions;
using RapidCli.Domain.Interfaces;
using RapidCli.Domain.Models;

namespace RapidCli.Application.Services;

/// <summary>
/// Coordinates the conversation flow between the CLI and the underlying AI provider.
/// </summary>
public sealed class ChatService
{
    private const int EstimatedTokensPerCharacter = 4;
    private const int ConversationTokenLimit = 6000;
    private const int PreserveTailMessageCount = 6;
    private const int ThoughtLogLimit = 50;

    private readonly IAIClient _client;
    private readonly ConversationManager _conversationManager;
    private readonly ConfigurationService _configurationService;
    private readonly SessionStorageService _sessionStorage;
    private readonly ILogger<ChatService> _logger;
    private readonly SemaphoreSlim _sessionSemaphore = new(1, 1);

    private ConversationSession? _currentSession;
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatService"/> class.
    /// </summary>
    /// <param name="client">The AI client implementation responsible for HTTP interactions.</param>
    /// <param name="conversationManager">The component that keeps track of the conversation history.</param>
    /// <param name="configurationService">The provider for configurable generation parameters.</param>
    /// <param name="sessionStorage">Component responsible for persisting sessions.</param>
    /// <param name="logger">The logger used for diagnostic messages.</param>
    public ChatService(
        IAIClient client,
        ConversationManager conversationManager,
        ConfigurationService configurationService,
        SessionStorageService sessionStorage,
        ILogger<ChatService> logger)
    {
        _client = client;
        _conversationManager = conversationManager;
        _configurationService = configurationService;
        _sessionStorage = sessionStorage;
        _logger = logger;
    }

    /// <summary>
    /// Gets the currently active session.
    /// </summary>
    public ConversationSession CurrentSession
        => _currentSession ?? throw new InvalidOperationException("The chat service has not been initialised.");

    /// <summary>
    /// Ensures that a session exists and is persisted on disk.
    /// </summary>
    public async Task InitializeSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _sessionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            _currentSession = _sessionStorage.CreateSession();
            _conversationManager.Clear();
            _initialized = true;
            RefreshConfigurationSnapshot();
            await PersistAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sessionSemaphore.Release();
        }
    }

    /// <summary>
    /// Starts a new session, optionally using the provided identifier.
    /// </summary>
    public async Task ResetAsync(string? sessionId = null, CancellationToken cancellationToken = default)
    {
        await _sessionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _currentSession = _sessionStorage.CreateSession(sessionId);
            _conversationManager.Clear();
            _initialized = true;
            RefreshConfigurationSnapshot();
            await PersistAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sessionSemaphore.Release();
        }
    }

    /// <summary>
    /// Loads the specified session from disk.
    /// </summary>
    /// <returns><c>true</c> when the session exists and was loaded successfully.</returns>
    public async Task<bool> LoadSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await InitializeSessionAsync(cancellationToken).ConfigureAwait(false);

        var loaded = await _sessionStorage.LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (loaded is null)
        {
            return false;
        }

        await _sessionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            NormalizeSession(loaded);
            _currentSession = loaded;
            _conversationManager.Load(loaded.Messages);
            await EnsureHistoryWithinLimitsAsync(cancellationToken).ConfigureAwait(false);
            RefreshConfigurationSnapshot();
            await PersistAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sessionSemaphore.Release();
        }

        return true;
    }

    /// <summary>
    /// Stores a copy of the current session using the provided identifier.
    /// </summary>
    /// <returns>The identifier used to store the snapshot.</returns>
    public async Task<string> SaveSnapshotAsync(string newIdentifier, CancellationToken cancellationToken = default)
    {
        await InitializeSessionAsync(cancellationToken).ConfigureAwait(false);
        var session = CurrentSession;
        return await _sessionStorage.SaveAsAsync(session, newIdentifier, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the conversation history.
    /// </summary>
    public IReadOnlyList<ChatMessage> GetHistory()
        => _conversationManager.Messages;

    /// <summary>
    /// Adds a user message to the history and persists the session.
    /// </summary>
    public Task AddUserMessageAsync(string content, CancellationToken cancellationToken = default)
        => AddMessageAsync(new ChatMessage { Role = "user", Content = content }, cancellationToken);

    /// <summary>
    /// Adds an assistant message to the history and persists the session.
    /// </summary>
    public Task AddAssistantMessageAsync(string content, CancellationToken cancellationToken = default)
        => AddMessageAsync(new ChatMessage { Role = "assistant", Content = content }, cancellationToken);

    /// <summary>
    /// Replaces the conversation history with the supplied messages and persists the session.
    /// </summary>
    public async Task SetHistoryAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        await InitializeSessionAsync(cancellationToken).ConfigureAwait(false);
        _conversationManager.Load(messages);
        await EnsureHistoryWithinLimitsAsync(cancellationToken).ConfigureAwait(false);
        await PersistAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Records a reasoning trace for the agent and persists it.
    /// </summary>
    public async Task RecordThoughtAsync(string thought, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(thought))
        {
            return;
        }

        await InitializeSessionAsync(cancellationToken).ConfigureAwait(false);
        AppendThoughtInternal(thought);
        await PersistAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Registers the usage of a tool during the current session.
    /// </summary>
    public void RegisterToolUsage(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName) || _currentSession is null)
        {
            return;
        }

        if (!_currentSession.ToolsUsed.Contains(toolName, StringComparer.OrdinalIgnoreCase))
        {
            _currentSession.ToolsUsed.Add(toolName);
        }

        if (!_currentSession.AgentState.ActiveTools.Contains(toolName, StringComparer.OrdinalIgnoreCase))
        {
            _currentSession.AgentState.ActiveTools.Add(toolName);
        }
    }

    /// <summary>
    /// Updates the agent state with the provided tool invocations.
    /// </summary>
    public void RegisterAgentToolInvocations(IEnumerable<AgentToolInvocation> invocations)
    {
        if (_currentSession is null)
        {
            return;
        }

        foreach (var invocation in invocations)
        {
            RegisterToolUsage(invocation.ToolName);
            foreach (var path in ExtractPaths(invocation))
            {
                if (!_currentSession.AgentState.LoadedFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    _currentSession.AgentState.LoadedFiles.Add(path);
                }
            }
        }
    }

    /// <summary>
    /// Sends a user message to the provider and returns the assistant response as an asynchronous stream.
    /// </summary>
    /// <param name="userMessage">The message provided by the user.</param>
    /// <param name="streamOverride">Optional override for the streaming configuration.</param>
    /// <param name="cancellationToken">The token used to cancel the request.</param>
    /// <returns>An asynchronous sequence containing fragments of the assistant response.</returns>
    public async IAsyncEnumerable<string> GetAssistantResponseAsync(
        string userMessage,
        bool? streamOverride = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await AddUserMessageAsync(userMessage, cancellationToken).ConfigureAwait(false);

        var request = BuildRequest(streamOverride);
        _logger.LogDebug("Prepared request for model {Model} with streaming {Stream}", request.Model, request.Stream);

        if (request.Stream)
        {
            var builder = new StringBuilder();
            await foreach (var chunk in _client.StreamChatCompletionAsync(request, cancellationToken).ConfigureAwait(false))
            {
                var content = chunk.Choices.FirstOrDefault()?.Delta?.Content;
                if (!string.IsNullOrEmpty(content))
                {
                    builder.Append(content);
                    yield return content;
                }

                var finishReason = chunk.Choices.FirstOrDefault()?.FinishReason;
                if (!string.IsNullOrEmpty(finishReason))
                {
                    break;
                }
            }

            await AddAssistantMessageAsync(builder.ToString(), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var response = await _client.CreateChatCompletionAsync(request, cancellationToken).ConfigureAwait(false);
            var text = response.Choices.FirstOrDefault()?.Message?.Content ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return text;
            }

            await AddAssistantMessageAsync(text, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        await InitializeSessionAsync(cancellationToken).ConfigureAwait(false);
        _conversationManager.AddMessage(message);
        await EnsureHistoryWithinLimitsAsync(cancellationToken).ConfigureAwait(false);
        await PersistAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureHistoryWithinLimitsAsync(CancellationToken cancellationToken)
    {
        if (_currentSession is null)
        {
            return;
        }

        var messages = _conversationManager.Messages;
        if (messages.Count == 0)
        {
            return;
        }

        var estimatedTokens = EstimateTokenCount(messages);
        if (estimatedTokens <= ConversationTokenLimit)
        {
            _currentSession.Messages = messages.ToList();
            return;
        }

        var preserveCount = Math.Min(PreserveTailMessageCount, messages.Count);
        var toSummarize = messages.Take(messages.Count - preserveCount).ToList();
        if (toSummarize.Count == 0)
        {
            _currentSession.Messages = messages.ToList();
            return;
        }

        var summary = await SummarizeHistoryAsync(toSummarize, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(summary))
        {
            _currentSession.Messages = messages.ToList();
            return;
        }

        var compactHistory = new List<ChatMessage>
        {
            new()
            {
                Role = "system",
                Content = $"Resumen del contexto: {summary}",
            },
        };
        compactHistory.AddRange(messages.Skip(messages.Count - preserveCount));

        _conversationManager.Load(compactHistory);
        _currentSession.AgentState.LastSummary = summary;
        AppendThoughtInternal($"Se generó un resumen automático que condensa {toSummarize.Count} mensajes previos.");
        _currentSession.Messages = _conversationManager.Messages.ToList();
    }

    private async Task<string?> SummarizeHistoryAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken)
    {
        try
        {
            var transcript = new StringBuilder();
            foreach (var message in messages)
            {
                if (string.IsNullOrWhiteSpace(message.Content))
                {
                    continue;
                }

                transcript.AppendLine($"[{message.Role}] {message.Content}");
            }

            var prompt = new List<ChatMessage>
            {
                new()
                {
                    Role = "system",
                    Content = "Eres un asistente que resume conversaciones técnicas para mantener el contexto relevante.",
                },
                new()
                {
                    Role = "user",
                    Content = "Resume la siguiente conversación en español resaltando decisiones, archivos y acciones clave:\n\n" + transcript,
                },
            };

            var config = _configurationService.Current;
            var request = new ChatCompletionRequest
            {
                Model = !string.IsNullOrWhiteSpace(config.Agent.Model) ? config.Agent.Model! : config.Model,
                Stream = false,
                Temperature = 0.2,
                TopP = 0.9,
                MaxTokens = 512,
                Messages = prompt,
            };

            var response = await _client.CreateChatCompletionAsync(request, cancellationToken).ConfigureAwait(false);
            return response.Choices.FirstOrDefault()?.Message?.Content;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to summarize conversation history");
            return null;
        }
    }

    private void RefreshConfigurationSnapshot()
    {
        if (_currentSession is null)
        {
            return;
        }

        var config = _configurationService.Current;
        var snapshot = _currentSession.AgentState.ConfigurationSnapshot;
        snapshot.Clear();
        snapshot["chat.model"] = config.Model;
        snapshot["chat.temperature"] = config.Temperature.ToString("0.###", CultureInfo.InvariantCulture);
        snapshot["chat.top_p"] = config.TopP.ToString("0.###", CultureInfo.InvariantCulture);
        snapshot["chat.max_tokens"] = config.MaxTokens.ToString(CultureInfo.InvariantCulture);
        snapshot["chat.frequency_penalty"] = config.FrequencyPenalty.ToString("0.###", CultureInfo.InvariantCulture);
        snapshot["chat.presence_penalty"] = config.PresencePenalty.ToString("0.###", CultureInfo.InvariantCulture);
        snapshot["chat.stream"] = config.Stream.ToString();
        snapshot["agent.enabled"] = config.Agent.Enabled.ToString();
        snapshot["agent.model"] = string.IsNullOrWhiteSpace(config.Agent.Model) ? "(heredado)" : config.Agent.Model!;
        snapshot["agent.max_iterations"] = config.Agent.MaxIterations.ToString(CultureInfo.InvariantCulture);
        snapshot["agent.allow_file_writes"] = config.Agent.AllowFileWrites.ToString();
        snapshot["agent.working_directory"] = string.IsNullOrWhiteSpace(config.Agent.WorkingDirectory)
            ? "."
            : config.Agent.WorkingDirectory!;
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        if (_currentSession is null)
        {
            return;
        }

        RefreshConfigurationSnapshot();
        _currentSession.AgentState.LastUpdated = DateTimeOffset.UtcNow;
        _currentSession.Messages = _conversationManager.Messages.ToList();
        await _sessionStorage.SaveAsync(_currentSession, cancellationToken).ConfigureAwait(false);
    }

    private static int EstimateTokenCount(IReadOnlyList<ChatMessage> messages)
    {
        var totalCharacters = messages.Sum(message => message.Content?.Length ?? 0);
        return totalCharacters / EstimatedTokensPerCharacter;
    }

    private static IEnumerable<string> ExtractPaths(AgentToolInvocation invocation)
    {
        if (string.IsNullOrWhiteSpace(invocation.Arguments))
        {
            yield break;
        }

        try
        {
            using var document = JsonDocument.Parse(invocation.Arguments);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                yield break;
            }

            if (document.RootElement.TryGetProperty("path", out var pathElement) && pathElement.ValueKind == JsonValueKind.String)
            {
                yield return pathElement.GetString()!;
            }

            if (document.RootElement.TryGetProperty("target", out var targetElement) && targetElement.ValueKind == JsonValueKind.String)
            {
                yield return targetElement.GetString()!;
            }
        }
        catch
        {
            yield break;
        }
    }

    private void AppendThoughtInternal(string thought)
    {
        if (_currentSession is null)
        {
            return;
        }

        var log = _currentSession.AgentState.ThoughtLog;
        log.Add(thought);
        while (log.Count > ThoughtLogLimit)
        {
            log.RemoveAt(0);
        }
    }

    private void NormalizeSession(ConversationSession session)
    {
        session.AgentState ??= new AgentState();
        session.AgentState.ThoughtLog ??= new List<string>();
        session.AgentState.ActiveTools ??= new List<string>();
        session.AgentState.LoadedFiles ??= new List<string>();
        session.AgentState.ConfigurationSnapshot ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        session.ToolsUsed ??= new List<string>();
        session.Messages ??= new List<ChatMessage>();
    }

    private ChatCompletionRequest BuildRequest(bool? streamOverride)
    {
        var config = _configurationService.Current;
        var request = new ChatCompletionRequest
        {
            Model = config.Model,
            Stream = streamOverride ?? config.Stream,
            Temperature = config.Temperature,
            TopP = config.TopP,
            MaxTokens = config.MaxTokens,
            FrequencyPenalty = config.FrequencyPenalty,
            PresencePenalty = config.PresencePenalty,
            Messages = _conversationManager.Messages.ToList(),
        };

        return request;
    }
}
