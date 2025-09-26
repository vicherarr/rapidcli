using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RapidCli.Domain.Interfaces;
using RapidCli.Domain.Models;

namespace RapidCli.Infrastructure.Clients;

/// <summary>
/// Concrete <see cref="IAIClient"/> implementation that communicates with the Chutes.ai API using Refit.
/// </summary>
public sealed class ChutesAiClient : IAIClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly IChutesAiApi _api;
    private readonly ILogger<ChutesAiClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChutesAiClient"/> class.
    /// </summary>
    /// <param name="api">The Refit generated API client.</param>
    /// <param name="logger">The logger used for diagnostics.</param>
    public ChutesAiClient(IChutesAiApi api, ILogger<ChutesAiClient> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> CreateChatCompletionAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        using var response = await _api.CreateChatCompletionAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var completion = await JsonSerializer.DeserializeAsync<ChatCompletionResponse>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        return completion ?? new ChatCompletionResponse();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(ChatCompletionRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request.Stream = true;
        using var response = await _api.CreateChatCompletionAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await foreach (var chunk in ReadStreamAsync(response, cancellationToken).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    private async IAsyncEnumerable<ChatCompletionChunk> ReadStreamAsync(HttpResponseMessage response, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                line = line[5..].TrimStart();
            }

            if (line.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            ChatCompletionChunk? chunk = null;
            try
            {
                chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(line, SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse streaming chunk: {Payload}", line);
            }

            if (chunk is not null)
            {
                yield return chunk;
            }
        }
    }
}
