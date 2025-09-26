using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RapidCli.Application.Configurations;
using RapidCli.Application.Services;
using RapidCli.Application.Sessions;
using RapidCli.Domain.Models;
using Spectre.Console;

namespace RapidCli.Cli;

/// <summary>
/// Handles the interactive command line experience for the assistant.
/// </summary>
public sealed class CliRunner
{
    private static readonly string[] KnownCommands =
    [
        "/exit",
        "/reset",
        "/config",
        "/history",
        "/save",
        "/load",
        "/sessions",
        "/help",
    ];

    private readonly ChatService _chatService;
    private readonly ConfigurationService _configurationService;
    private readonly SessionStorageService _sessionStorage;
    private readonly ILogger<CliRunner> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliRunner"/> class.
    /// </summary>
    public CliRunner(
        ChatService chatService,
        ConfigurationService configurationService,
        SessionStorageService sessionStorage,
        ILogger<CliRunner> logger)
    {
        _chatService = chatService;
        _configurationService = configurationService;
        _sessionStorage = sessionStorage;
        _logger = logger;
    }

    /// <summary>
    /// Starts the interactive session.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the session.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await _configurationService.ReloadAsync().ConfigureAwait(false);
        RenderWelcome();

        while (!cancellationToken.IsCancellationRequested)
        {
            var input = PromptUser();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.StartsWith('/'))
            {
                if (await HandleCommandAsync(input, cancellationToken).ConfigureAwait(false))
                {
                    break;
                }

                continue;
            }

            await RenderAssistantResponseAsync(input, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string PromptUser()
    {
        var prompt = new TextPrompt<string>("[bold green]You[/]> ")
            .AllowEmpty()
            .PromptStyle("white")
            .DefaultValue(string.Empty);
        return AnsiConsole.Prompt(prompt);
    }

    private async Task<bool> HandleCommandAsync(string rawInput, CancellationToken cancellationToken)
    {
        var tokens = rawInput.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        var normalized = NormalizeCommand(tokens[0]);
        var arguments = tokens.Skip(1).ToArray();

        switch (normalized)
        {
            case "/exit":
                AnsiConsole.MarkupLine("[bold yellow]Hasta pronto![/]");
                return true;
            case "/reset":
                _chatService.Reset();
                AnsiConsole.MarkupLine("[green]Contexto limpiado.[/]");
                return false;
            case "/history":
                RenderHistory();
                return false;
            case "/help":
                RenderHelp();
                return false;
            case "/config":
                await HandleConfigAsync(arguments).ConfigureAwait(false);
                return false;
            case "/save":
                await HandleSaveAsync(arguments).ConfigureAwait(false);
                return false;
            case "/load":
                await HandleLoadAsync(arguments).ConfigureAwait(false);
                return false;
            case "/sessions":
                RenderSessions();
                return false;
            default:
                AnsiConsole.MarkupLine("[red]Comando desconocido.[/]");
                return false;
        }
    }

    private string NormalizeCommand(string command)
    {
        var exact = KnownCommands.FirstOrDefault(cmd => string.Equals(cmd, command, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var match = KnownCommands.Where(cmd => cmd.StartsWith(command, StringComparison.OrdinalIgnoreCase)).ToList();
        if (match.Count == 1)
        {
            return match[0];
        }

        return command;
    }

    private async Task RenderAssistantResponseAsync(string userMessage, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"[bold green]You[/]: {Markup.Escape(userMessage)}");
        AnsiConsole.Markup("[bold yellow]Assistant[/]: ");

        try
        {
            var builder = new StringBuilder();
            await foreach (var fragment in _chatService.GetAssistantResponseAsync(userMessage, null, cancellationToken).ConfigureAwait(false))
            {
                if (string.IsNullOrEmpty(fragment))
                {
                    continue;
                }

                builder.Append(fragment);
                AnsiConsole.Markup(Markup.Escape(fragment));
            }

            if (builder.Length == 0)
            {
                AnsiConsole.Markup("[grey]No se recibió respuesta.[/]");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while obtaining assistant response");
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
        }
        finally
        {
            AnsiConsole.WriteLine();
        }
    }

    private void RenderHistory()
    {
        var history = _chatService.GetHistory();
        if (history.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No hay historial disponible.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("Rol").Centered());
        table.AddColumn(new TableColumn("Contenido"));

        foreach (var message in history)
        {
            table.AddRow(message.Role, Markup.Escape(message.Content));
        }

        AnsiConsole.Write(table);
    }

    private void RenderHelp()
    {
        var table = new Table().Border(TableBorder.Simple);
        table.AddColumn("Comando");
        table.AddColumn("Descripción");

        table.AddRow("/exit", "Cerrar la aplicación");
        table.AddRow("/reset", "Limpiar el contexto actual");
        table.AddRow("/history", "Mostrar el historial de mensajes");
        table.AddRow("/config", "Ver o actualizar parámetros");
        table.AddRow("/save <nombre>", "Guardar la sesión actual");
        table.AddRow("/load <nombre>", "Cargar una sesión guardada");
        table.AddRow("/sessions", "Listar sesiones disponibles");

        AnsiConsole.MarkupLine("[bold cyan]Comandos disponibles:[/]");
        AnsiConsole.Write(table);
    }

    private async Task HandleConfigAsync(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            RenderConfiguration(_configurationService.Current);
            return;
        }

        if (arguments.Count >= 2 && string.Equals(arguments[0], "set", StringComparison.OrdinalIgnoreCase))
        {
            var key = arguments[1].ToLowerInvariant();
            var value = arguments.Count > 2 ? string.Join(' ', arguments.Skip(2)) : string.Empty;

            await _configurationService.UpdateAsync(config =>
            {
                switch (key)
                {
                    case "model":
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            config.Model = value;
                        }

                        break;
                    case "temperature":
                        if (double.TryParse(value, out var temp))
                        {
                            config.Temperature = temp;
                        }

                        break;
                    case "top_p":
                        if (double.TryParse(value, out var topP))
                        {
                            config.TopP = topP;
                        }

                        break;
                    case "max_tokens":
                        if (int.TryParse(value, out var maxTokens))
                        {
                            config.MaxTokens = maxTokens;
                        }

                        break;
                    case "frequency_penalty":
                        if (double.TryParse(value, out var frequency))
                        {
                            config.FrequencyPenalty = frequency;
                        }

                        break;
                    case "presence_penalty":
                        if (double.TryParse(value, out var presence))
                        {
                            config.PresencePenalty = presence;
                        }

                        break;
                    case "stream":
                        if (bool.TryParse(value, out var stream))
                        {
                            config.Stream = stream;
                        }

                        break;
                }
            }).ConfigureAwait(false);

            RenderConfiguration(_configurationService.Current);
            return;
        }

        if (arguments.Count == 1 && string.Equals(arguments[0], "reload", StringComparison.OrdinalIgnoreCase))
        {
            await _configurationService.ReloadAsync().ConfigureAwait(false);
            RenderConfiguration(_configurationService.Current);
            return;
        }

        AnsiConsole.MarkupLine("[red]Formato de comando no reconocido.[/]");
    }

    private static void RenderConfiguration(ChatConfiguration configuration)
    {
        var table = new Table().Border(TableBorder.Simple);
        table.AddColumn("Clave");
        table.AddColumn("Valor");
        table.AddRow("Model", configuration.Model);
        table.AddRow("Temperature", configuration.Temperature.ToString("0.###"));
        table.AddRow("TopP", configuration.TopP.ToString("0.###"));
        table.AddRow("MaxTokens", configuration.MaxTokens.ToString());
        table.AddRow("FrequencyPenalty", configuration.FrequencyPenalty.ToString("0.###"));
        table.AddRow("PresencePenalty", configuration.PresencePenalty.ToString("0.###"));
        table.AddRow("Stream", configuration.Stream.ToString());

        AnsiConsole.MarkupLine("[bold cyan]Configuración actual:[/]");
        AnsiConsole.Write(table);
    }

    private async Task HandleSaveAsync(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Debes proporcionar un nombre de sesión.[/]");
            return;
        }

        var sessionName = arguments[0];
        await _sessionStorage.SaveAsync(sessionName, _chatService.GetHistory()).ConfigureAwait(false);
        AnsiConsole.MarkupLine($"[green]Sesión '{Markup.Escape(sessionName)}' guardada correctamente.[/]");
    }

    private async Task HandleLoadAsync(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Debes indicar el nombre de la sesión a cargar.[/]");
            return;
        }

        var sessionName = arguments[0];
        var messages = await _sessionStorage.LoadAsync(sessionName).ConfigureAwait(false);
        if (messages.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]La sesión no contiene mensajes o no existe.[/]");
            return;
        }

        _chatService.LoadHistory(messages);
        AnsiConsole.MarkupLine($"[green]Sesión '{Markup.Escape(sessionName)}' cargada.[/]");
    }

    private void RenderSessions()
    {
        var sessions = _sessionStorage.ListSessions();
        if (sessions.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No hay sesiones guardadas.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded).Title("Sesiones disponibles");
        table.AddColumn("Nombre");
        foreach (var session in sessions)
        {
            table.AddRow(Markup.Escape(session));
        }

        AnsiConsole.Write(table);
    }

    private static void RenderWelcome()
    {
        var rule = new Rule("[bold magenta]RapidCLI Assistant[/]")
        {
            Justification = Justify.Center,
        };
        AnsiConsole.Write(rule);
        AnsiConsole.MarkupLine("[grey]Escribe tu mensaje o usa [u]/help[/] para ver comandos disponibles.[/]");
    }
}
