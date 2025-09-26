using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RapidCli.Application.Configurations;

namespace RapidCli.Application.Services;

/// <summary>
/// Manages persistence and retrieval of chat related configuration values.
/// </summary>
public sealed class ConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _configurationFilePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationService"/> class.
    /// </summary>
    /// <param name="options">The configuration values captured from the application settings.</param>
    /// <param name="logger">The logger used for diagnostic messages.</param>
    public ConfigurationService(IOptions<ChatConfiguration> options, ILogger<ConfigurationService> logger)
    {
        _logger = logger;
        Current = options.Value;
        _configurationFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".rapidcli", "config.json");
    }

    /// <summary>
    /// Gets the current configuration in memory.
    /// </summary>
    public ChatConfiguration Current { get; private set; }

    /// <summary>
    /// Updates the configuration using the provided delegate and persists the changes to disk.
    /// </summary>
    /// <param name="updater">The action that applies modifications to the configuration.</param>
    public async Task UpdateAsync(Action<ChatConfiguration> updater)
    {
        ArgumentNullException.ThrowIfNull(updater);

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            updater(Current);
            await PersistAsync().ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Reloads the configuration from disk if an override file exists.
    /// </summary>
    public async Task ReloadAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_configurationFilePath))
            {
                return;
            }

            var json = await File.ReadAllTextAsync(_configurationFilePath).ConfigureAwait(false);
            var restored = JsonSerializer.Deserialize<ChatConfiguration>(json);
            if (restored is not null)
            {
                Current = restored;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload chat configuration from {File}", _configurationFilePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task PersistAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_configurationFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            await File.WriteAllTextAsync(_configurationFilePath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist chat configuration to {File}", _configurationFilePath);
        }
    }
}
