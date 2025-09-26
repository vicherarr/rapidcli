namespace RapidCli.Infrastructure.Options;

/// <summary>
/// Represents configuration options for the Chutes.ai provider.
/// </summary>
public sealed class ChutesAiOptions
{
    /// <summary>
    /// Gets or sets the base URL for the Chutes.ai API.
    /// </summary>
    public string BaseUrl { get; set; } = "https://llm.chutes.ai";

    /// <summary>
    /// Gets or sets the API token used for authentication.
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;
}
