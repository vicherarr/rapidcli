using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Logging;
using RapidCli.Domain.Models;

namespace RapidCli.Application.Sessions;

/// <summary>
/// Provides utilities to persist and restore chat sessions on disk.
/// </summary>
public sealed class SessionStorageService
{
    private readonly ILogger<SessionStorageService> _logger;
    private readonly string _sessionsDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionStorageService"/> class.
    /// </summary>
    /// <param name="logger">The logger used for diagnostic messages.</param>
    public SessionStorageService(ILogger<SessionStorageService> logger)
    {
        _logger = logger;
        _sessionsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".rapidcli", "sessions");
    }

    /// <summary>
    /// Saves the provided messages under the given session name.
    /// </summary>
    /// <param name="sessionName">The identifier of the session.</param>
    /// <param name="messages">The messages to persist.</param>
    public async Task SaveAsync(string sessionName, IEnumerable<ChatMessage> messages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionName);

        try
        {
            Directory.CreateDirectory(_sessionsDirectory);
            var path = GetSessionFile(sessionName);
            var json = JsonSerializer.Serialize(messages, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save session {Session}", sessionName);
            throw;
        }
    }

    /// <summary>
    /// Loads a previously saved session.
    /// </summary>
    /// <param name="sessionName">The identifier of the session.</param>
    /// <returns>The stored messages or an empty list if the session does not exist.</returns>
    public async Task<IReadOnlyList<ChatMessage>> LoadAsync(string sessionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionName);

        try
        {
            var path = GetSessionFile(sessionName);
            if (!File.Exists(path))
            {
                return Array.Empty<ChatMessage>();
            }

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var messages = JsonSerializer.Deserialize<IReadOnlyList<ChatMessage>>(json);
            return messages ?? Array.Empty<ChatMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load session {Session}", sessionName);
            throw;
        }
    }

    /// <summary>
    /// Lists the names of the available saved sessions.
    /// </summary>
    public IReadOnlyList<string> ListSessions()
    {
        if (!Directory.Exists(_sessionsDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory
            .EnumerateFiles(_sessionsDirectory, "*.json")
            .Select(file => Path.GetFileNameWithoutExtension(file))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string GetSessionFile(string sessionName)
        => Path.Combine(_sessionsDirectory, $"{sessionName}.json");
}
