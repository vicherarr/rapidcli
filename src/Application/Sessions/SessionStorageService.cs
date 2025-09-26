using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RapidCli.Domain.Models;

namespace RapidCli.Application.Sessions;

/// <summary>
/// Provides utilities to persist and restore chat sessions on disk.
/// </summary>
public sealed class SessionStorageService
{
    private const string SessionsDirectoryName = "sessions";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ILogger<SessionStorageService> _logger;
    private readonly string _sessionsDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionStorageService"/> class.
    /// </summary>
    /// <param name="logger">The logger used for diagnostic messages.</param>
    public SessionStorageService(ILogger<SessionStorageService> logger)
    {
        _logger = logger;
        _sessionsDirectory = Path.Combine(Directory.GetCurrentDirectory(), SessionsDirectoryName);
    }

    /// <summary>
    /// Creates a new session descriptor with the specified identifier.
    /// </summary>
    /// <param name="sessionId">Optional identifier to associate with the session.</param>
    public ConversationSession CreateSession(string? sessionId = null)
    {
        Directory.CreateDirectory(_sessionsDirectory);

        var id = !string.IsNullOrWhiteSpace(sessionId)
            ? SanitizeSessionId(sessionId)
            : GenerateSessionId();

        var now = DateTimeOffset.UtcNow;
        return new ConversationSession
        {
            Id = id,
            CreatedAt = now,
            UpdatedAt = now,
            AgentState = new AgentState(),
        };
    }

    /// <summary>
    /// Persists the provided session to disk.
    /// </summary>
    /// <param name="session">The session to store.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task SaveAsync(ConversationSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(session.Id);

        try
        {
            Directory.CreateDirectory(_sessionsDirectory);
            session.UpdatedAt = DateTimeOffset.UtcNow;
            var json = JsonSerializer.Serialize(session, SerializerOptions);
            var path = GetSessionFile(session.Id);
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save session {Session}", session.Id);
            throw;
        }
    }

    /// <summary>
    /// Creates a copy of the provided session using the specified identifier.
    /// </summary>
    /// <returns>The normalized identifier used to store the snapshot.</returns>
    public async Task<string> SaveAsAsync(ConversationSession session, string newIdentifier, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(newIdentifier);

        var sanitized = SanitizeSessionId(newIdentifier);
        var clone = new ConversationSession
        {
            Id = sanitized,
            CreatedAt = session.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
            Messages = session.Messages.ToList(),
            AgentState = session.AgentState ?? new AgentState(),
            ToolsUsed = session.ToolsUsed.ToList(),
        };

        await SaveAsync(clone, cancellationToken).ConfigureAwait(false);
        return sanitized;
    }

    /// <summary>
    /// Loads a previously saved session.
    /// </summary>
    /// <param name="sessionId">The identifier of the session.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The stored session or <c>null</c> if the session does not exist.</returns>
    public async Task<ConversationSession?> LoadAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        try
        {
            var path = GetSessionFile(sessionId);
            if (!File.Exists(path))
            {
                return null;
            }

            await using var stream = File.OpenRead(path);
            var session = await JsonSerializer
                .DeserializeAsync<ConversationSession>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (session is not null)
            {
                session.Id = SanitizeSessionId(session.Id);
                session.AgentState ??= new AgentState();
                session.ToolsUsed ??= new List<string>();
                session.Messages ??= new List<ChatMessage>();
                return session;
            }

            // Compatibility fallback with legacy format that stored only chat messages.
            stream.Position = 0;
            var legacyMessages = await JsonSerializer
                .DeserializeAsync<IReadOnlyList<ChatMessage>>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (legacyMessages is null)
            {
                return null;
            }

            var info = new FileInfo(path);
            return new ConversationSession
            {
                Id = SanitizeSessionId(sessionId),
                CreatedAt = info.CreationTimeUtc,
                UpdatedAt = info.LastWriteTimeUtc,
                Messages = legacyMessages.ToList(),
                AgentState = new AgentState(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load session {Session}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Lists the names of the available saved sessions.
    /// </summary>
    public IReadOnlyList<ConversationSessionSummary> ListSessions()
    {
        if (!Directory.Exists(_sessionsDirectory))
        {
            return Array.Empty<ConversationSessionSummary>();
        }

        var summaries = new List<ConversationSessionSummary>();
        foreach (var file in Directory.EnumerateFiles(_sessionsDirectory, "*.json"))
        {
            try
            {
                using var stream = File.OpenRead(file);
                using var document = JsonDocument.Parse(stream);
                var root = document.RootElement;

                var id = root.TryGetProperty("id", out var idElement)
                    ? SanitizeSessionId(idElement.GetString() ?? Path.GetFileNameWithoutExtension(file))
                    : Path.GetFileNameWithoutExtension(file);

                var createdAt = root.TryGetProperty("createdAt", out var createdElement)
                    ? createdElement.GetDateTimeOffset()
                    : File.GetCreationTimeUtc(file);

                var updatedAt = root.TryGetProperty("updatedAt", out var updatedElement)
                    ? updatedElement.GetDateTimeOffset()
                    : File.GetLastWriteTimeUtc(file);

                var messageCount = root.TryGetProperty("messages", out var messagesElement) && messagesElement.ValueKind == JsonValueKind.Array
                    ? messagesElement.GetArrayLength()
                    : 0;

                var toolCount = root.TryGetProperty("toolsUsed", out var toolsElement) && toolsElement.ValueKind == JsonValueKind.Array
                    ? toolsElement.GetArrayLength()
                    : 0;

                summaries.Add(new ConversationSessionSummary
                {
                    Id = id,
                    CreatedAt = createdAt,
                    UpdatedAt = updatedAt,
                    MessageCount = messageCount,
                    ToolCount = toolCount,
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse session metadata from {File}", file);
            }
        }

        return summaries
            .OrderByDescending(summary => summary.UpdatedAt)
            .ThenBy(summary => summary.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string GetSessionFile(string sessionId)
    {
        var sanitized = SanitizeSessionId(sessionId);
        return Path.Combine(_sessionsDirectory, $"{sanitized}.json");
    }

    private static string GenerateSessionId()
    {
        var guidSegment = Guid.NewGuid().ToString("N")[..6];
        return $"session-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{guidSegment}";
    }

    private static string SanitizeSessionId(string sessionId)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(sessionId.Where(ch => !invalidChars.Contains(ch)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? GenerateSessionId() : cleaned;
    }
}
