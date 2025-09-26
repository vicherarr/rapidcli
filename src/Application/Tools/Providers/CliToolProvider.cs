using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RapidCli.Domain.Interfaces;
using RapidCli.Domain.Models;

namespace RapidCli.Application.Tools.Providers;

/// <summary>
/// Executes tools that expose a command line interface.
/// </summary>
public sealed class CliToolProvider : IAgentToolProvider
{
    /// <inheritdoc />
    public string Name => "cli";

    /// <inheritdoc />
    public bool CanHandle(McpToolConfiguration configuration)
        => string.Equals(configuration.Execution.Mode, "cli", StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(configuration.Execution.Command);

    /// <inheritdoc />
    public async Task<ToolAvailability> GetAvailabilityAsync(McpToolConfiguration configuration, CancellationToken cancellationToken)
    {
        var command = configuration.Execution.Command;
        if (string.IsNullOrWhiteSpace(command))
        {
            return ToolAvailability.Unavailable("Comando no configurado.");
        }

        var location = await LocateExecutableAsync(command, cancellationToken).ConfigureAwait(false);
        return location is not null
            ? ToolAvailability.Available(location)
            : ToolAvailability.Unavailable($"No se encontró el ejecutable '{command}'.");
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteAsync(McpToolConfiguration configuration, McpToolInvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(context);

        var command = configuration.Execution.Command;
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new InvalidOperationException("El comando de la herramienta no está configurado.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in ExpandArguments(configuration.Execution.Arguments, context.Parameters))
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrWhiteSpace(configuration.Execution.WorkingDirectory))
        {
            var workingDirectory = ResolveWorkingDirectory(configuration.Execution.WorkingDirectory);
            if (!Directory.Exists(workingDirectory))
            {
                Directory.CreateDirectory(workingDirectory);
            }

            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var pair in configuration.Execution.Environment)
        {
            startInfo.Environment[pair.Key] = ResolveTokens(pair.Value, context.Parameters);
        }

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        var stopwatch = Stopwatch.StartNew();

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                outputBuilder.AppendLine(args.Data);
            }
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                errorBuilder.AppendLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        var output = outputBuilder.ToString().Trim();
        var error = errorBuilder.ToString().Trim();

        return process.ExitCode == 0
            ? ToolExecutionResult.SuccessResult(output, string.IsNullOrWhiteSpace(error) ? null : error, stopwatch.Elapsed)
            : ToolExecutionResult.FailureResult(output, string.IsNullOrWhiteSpace(error) ? null : error, stopwatch.Elapsed);
    }

    private static IEnumerable<string> ExpandArguments(IEnumerable<string> arguments, IReadOnlyDictionary<string, string> parameters)
    {
        foreach (var argument in arguments)
        {
            yield return ResolveTokens(argument, parameters);
        }
    }

    private static string ResolveTokens(string value, IReadOnlyDictionary<string, string> parameters)
    {
        var result = value;
        foreach (var pair in parameters)
        {
            result = result.Replace($"{{{pair.Key}}}", pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string ResolveWorkingDirectory(string path)
        => Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));

    private static async Task<string?> LocateExecutableAsync(string command, CancellationToken cancellationToken)
    {
        var lookupCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = lookupCommand,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.StartInfo.ArgumentList.Add(command);
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode == 0)
            {
                var firstLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return firstLine;
            }
        }
        catch
        {
            // Ignored: availability checks are best effort.
        }

        return null;
    }
}
