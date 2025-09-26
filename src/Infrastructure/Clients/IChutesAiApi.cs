using System.Net.Http;
using Refit;
using RapidCli.Domain.Models;

namespace RapidCli.Infrastructure.Clients;

/// <summary>
/// Defines the REST contract for the Chutes.ai chat completion endpoint.
/// </summary>
public interface IChutesAiApi
{
    /// <summary>
    /// Creates a chat completion using the provided request payload.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>The HTTP response message produced by the server.</returns>
    [Post("/v1/chat/completions")]
    Task<HttpResponseMessage> CreateChatCompletionAsync(
        [Body] ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}
