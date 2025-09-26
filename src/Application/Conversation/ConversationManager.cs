using System.Linq;
using RapidCli.Domain.Models;

namespace RapidCli.Application.Conversation;

/// <summary>
/// Provides utilities for managing chat history.
/// </summary>
public sealed class ConversationManager
{
    private readonly IList<ChatMessage> _messages = new List<ChatMessage>();

    /// <summary>
    /// Gets the current messages in the conversation.
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages => _messages.ToList();

    /// <summary>
    /// Appends a message to the history.
    /// </summary>
    /// <param name="message">The message to store.</param>
    public void AddMessage(ChatMessage message)
    {
        _messages.Add(message);
    }

    /// <summary>
    /// Clears the stored history.
    /// </summary>
    public void Clear()
    {
        _messages.Clear();
    }

    /// <summary>
    /// Loads a pre-existing conversation, replacing any previously stored messages.
    /// </summary>
    /// <param name="messages">The collection of messages to load.</param>
    public void Load(IEnumerable<ChatMessage> messages)
    {
        _messages.Clear();
        foreach (var message in messages)
        {
            _messages.Add(message);
        }
    }
}
