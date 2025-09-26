using System;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RapidCli.Domain.Interfaces;
using RapidCli.Infrastructure.Clients;
using RapidCli.Infrastructure.Constants;
using RapidCli.Infrastructure.Options;
using Refit;

namespace RapidCli.Infrastructure.Extensions;

/// <summary>
/// Registers infrastructure services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds infrastructure dependencies to the container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ChutesAiOptions>(configuration.GetSection("ChutesAi"));

        services
            .AddRefitClient<IChutesAiApi>()
            .ConfigureHttpClient((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<ChutesAiOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                var apiToken = options.ApiToken;

                if (string.IsNullOrWhiteSpace(apiToken))
                {
                    apiToken = Environment.GetEnvironmentVariable(EnvironmentVariableNames.ChutesApiKey);
                }

                if (!string.IsNullOrWhiteSpace(apiToken))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
                }
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            });

        services.AddSingleton<IAIClient, ChutesAiClient>();

        return services;
    }
}
