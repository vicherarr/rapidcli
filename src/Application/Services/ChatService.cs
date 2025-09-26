using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using RapidCli.Application.Configurations;
using RapidCli.Application.Conversation;
using RapidCli.Domain.Interfaces;
using RapidCli.Domain.Models;

namespace RapidCli.Application.Services;

/// <summary>
/// Coordinates the conversation flow between the CLI and the underlying AI provider.
/// </summary>
public sealed class ChatService
{
    private readonly IAIClient _client;
    private readonly ConversationManager _conversationManager;
    private readonly ConfigurationService _configurationService;
    private readonly ILogger<ChatService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatService"/> class.
    /// </summary>
    /// <param name="client">The AI client implementation responsible for HTTP interactions.</param>
    /// <param name="conversationManager">The component that keeps track of the conversation history.</param>
    /// <param name="configurationService">The provider for configurable generation parameters.</param>
    /// <param name="logger">The logger used for diagnostic messages.</param>
    public ChatService(
        IAIClient client,
        ConversationManager conversationManager,
        ConfigurationService configurationService,
        ILogger<ChatService> logger)
    {
        _client = client;
        _conversationManager = conversationManager;
        _configurationService = configurationService;
        _logger = logger;
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
        _logger.LogInformation("Dispatching user message with {TokenCount} characters", userMessage.Length);

        var message = new ChatMessage
        {
            Role = "user",
            Content = userMessage,
        };
        _conversationManager.AddMessage(message);

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

            var assistantMessage = new ChatMessage
            {
                Role = "assistant",
                Content = builder.ToString(),
            };
            _conversationManager.AddMessage(assistantMessage);
        }
        else
        {
            var response = await _client.CreateChatCompletionAsync(request, cancellationToken).ConfigureAwait(false);
            var text = response.Choices.FirstOrDefault()?.Message?.Content ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return text;
            }

            var assistantMessage = new ChatMessage
            {
                Role = "assistant",
                Content = text,
            };
            _conversationManager.AddMessage(assistantMessage);
        }
    }

    /// <summary>
    /// Gets the conversation history.
    /// </summary>
    public IReadOnlyList<ChatMessage> GetHistory()
        => _conversationManager.Messages;

    /// <summary>
    /// Clears the conversation history.
    /// </summary>
    public void Reset()
        => _conversationManager.Clear();

    /// <summary>
    /// Replaces the conversation history with the supplied messages.
    /// </summary>
    /// <param name="messages">The messages that should become the active conversation.</param>
    public void LoadHistory(IEnumerable<ChatMessage> messages)
        => _conversationManager.Load(messages);

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
