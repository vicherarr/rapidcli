using RapidCli.Domain.Models;

namespace RapidCli.Domain.Interfaces;

/// <summary>
/// Defines a contract for interacting with large language model providers.
/// </summary>
public interface IAIClient
{
    /// <summary>
    /// Creates a chat completion using the provided request payload.
    /// </summary>
    /// <param name="request">The payload that describes the conversation and generation parameters.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A fully materialized completion response from the provider.</returns>
    Task<ChatCompletionResponse> CreateChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a chat completion response using server sent events semantics.
    /// </summary>
    /// <param name="request">The payload that describes the conversation and generation parameters.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>An asynchronous sequence containing incremental completion chunks.</returns>
    IAsyncEnumerable<ChatCompletionChunk> StreamChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}
