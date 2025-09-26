using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RapidCli.Application.Agents;
using RapidCli.Application.Configurations;
using RapidCli.Application.Services;
using RapidCli.Application.Sessions;
using RapidCli.Application.Tools;
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
        "/agent",
        "/tools",
        "/mcp",
    ];

    private readonly ChatService _chatService;
    private readonly ConfigurationService _configurationService;
    private readonly SessionStorageService _sessionStorage;
    private readonly AgentService _agentService;
    private readonly ToolOrchestrator _toolOrchestrator;
    private readonly ILogger<CliRunner> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliRunner"/> class.
    /// </summary>
    public CliRunner(
        ChatService chatService,
        ConfigurationService configurationService,
        SessionStorageService sessionStorage,
        AgentService agentService,
        ToolOrchestrator toolOrchestrator,
        ILogger<CliRunner> logger)
    {
        _chatService = chatService;
        _configurationService = configurationService;
        _sessionStorage = sessionStorage;
        _agentService = agentService;
        _toolOrchestrator = toolOrchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Starts the interactive session.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the session.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await _configurationService.ReloadAsync().ConfigureAwait(false);
        await _toolOrchestrator.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _chatService.InitializeSessionAsync(cancellationToken).ConfigureAwait(false);
        RenderWelcome();
        RenderSessionInfo();

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
                await _chatService.ResetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                AnsiConsole.MarkupLine("[green]Contexto limpiado y nueva sesión creada.[/]");
                RenderSessionInfo();
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
                await HandleSaveAsync(arguments, cancellationToken).ConfigureAwait(false);
                return false;
            case "/load":
                await HandleLoadAsync(arguments, cancellationToken).ConfigureAwait(false);
                return false;
            case "/sessions":
                RenderSessions();
                return false;
            case "/agent":
                await HandleAgentAsync(arguments, cancellationToken).ConfigureAwait(false);
                return false;
            case "/tools":
            case "/mcp":
                RenderToolRegistry();
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
        await _chatService.AddUserMessageAsync(userMessage, cancellationToken).ConfigureAwait(false);

        AnsiConsole.MarkupLine($"[bold green]You[/]: {Markup.Escape(userMessage)}");
        AnsiConsole.MarkupLine("[bold yellow]Assistant[/]:");

        await RenderThinkingAsync("Analizando si es necesario invocar herramientas MCP antes de responder...", cancellationToken).ConfigureAwait(false);

        var orchestration = await _toolOrchestrator.TryOrchestrateAsync(userMessage, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(orchestration.Message))
        {
            AnsiConsole.MarkupLine($"[italic]{Markup.Escape(orchestration.Message)}[/]");
            await _chatService.RecordThoughtAsync(orchestration.Message!, cancellationToken).ConfigureAwait(false);
        }

        if (orchestration.ToolExecuted)
        {
            if (orchestration.Descriptor is not null)
            {
                _chatService.RegisterToolUsage(orchestration.Descriptor.DisplayName);
            }

            if (orchestration.ExecutionResult is not null)
            {
                await RenderThinkingAsync(
                    $"Se procesó la herramienta '{orchestration.Descriptor?.DisplayName ?? "desconocida"}'. Evaluando la información obtenida...",
                    cancellationToken).ConfigureAwait(false);
            }

            RenderOrchestratedTool(orchestration);
        }

        if (!_configurationService.Current.Agent.Enabled)
        {
            const string disabledMessage = "El agente está deshabilitado en la configuración actual.";
            AnsiConsole.MarkupLine($"[yellow]{disabledMessage} Usa /config set agent.enabled true para activarlo.[/]");
            await _chatService.AddAssistantMessageAsync(disabledMessage, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (orchestration.BypassAgent)
        {
            var response = orchestration.ResponseText ?? string.Empty;
            var panel = new Panel(Markup.Escape(response))
            {
                Header = new PanelHeader(orchestration.Descriptor?.DisplayName ?? "Resultado", Justify.Center),
                Border = BoxBorder.Rounded,
            };
            AnsiConsole.Write(panel);

            await _chatService.AddAssistantMessageAsync(response, cancellationToken).ConfigureAwait(false);
            return;
        }

        await RenderThinkingAsync("Preparando el objetivo para el agente autónomo y planificando los próximos pasos...", cancellationToken).ConfigureAwait(false);

        var agentObjective = orchestration.AgentObjective;
        AgentExecutionResult? result = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("[cyan]Ejecutando agente...[/]", async ctx =>
            {
                ctx.Status("Solicitando al modelo...");
                result = await _agentService.ExecuteTaskAsync(agentObjective, cancellationToken).ConfigureAwait(false);
            });

        if (result is null)
        {
            const string noResultMessage = "El agente no devolvió ningún resultado.";
            AnsiConsole.MarkupLine($"[red]{noResultMessage}[/]");
            await _chatService.AddAssistantMessageAsync(noResultMessage, cancellationToken).ConfigureAwait(false);
            return;
        }

        RenderAgentResult(result);
        _chatService.RegisterAgentToolInvocations(result.ToolInvocations);
        await _chatService.AddAssistantMessageAsync(result.FinalResponse, cancellationToken).ConfigureAwait(false);
    }

    private async Task RenderThinkingAsync(string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        AnsiConsole.MarkupLine($"[grey italic]{Markup.Escape(message)}[/]");
        await _chatService.RecordThoughtAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private void RenderOrchestratedTool(ToolOrchestrationResult orchestration)
    {
        if (orchestration.Descriptor is null || orchestration.ExecutionResult is null)
        {
            return;
        }

        var table = new Table().Border(TableBorder.Rounded).Title("Ejecución de herramientas MCP");
        table.AddColumn("Herramienta");
        table.AddColumn("Estado");
        table.AddColumn("Duración");
        table.AddColumn("Salida");

        var status = orchestration.ExecutionResult.Success ? "[green]OK[/]" : "[red]Error[/]";
        var preview = orchestration.ExecutionResult.Output;
        if (!string.IsNullOrWhiteSpace(preview) && preview.Length > 200)
        {
            preview = preview[..200] + "…";
        }

        table.AddRow(
            Markup.Escape(orchestration.Descriptor.DisplayName),
            status,
            orchestration.ExecutionResult.Duration.ToString("g"),
            string.IsNullOrWhiteSpace(preview) ? "[grey]<sin salida>[/]" : Markup.Escape(preview));

        AnsiConsole.Write(table);
    }

    private void RenderToolRegistry()
    {
        var tools = _toolOrchestrator.GetRegisteredTools();
        if (tools.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No hay herramientas MCP configuradas.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded).Title("Herramientas MCP disponibles");
        table.AddColumn("Nombre");
        table.AddColumn("Tipo");
        table.AddColumn("Tareas");
        table.AddColumn("Lenguajes");
        table.AddColumn("Estado");

        foreach (var descriptor in tools)
        {
            var tasks = descriptor.Configuration.Tasks.Count > 0
                ? string.Join(", ", descriptor.Configuration.Tasks)
                : "-";
            var languages = descriptor.Configuration.Languages.Count > 0
                ? string.Join(", ", descriptor.Configuration.Languages)
                : "-";
            var status = descriptor.Availability.IsAvailable
                ? "[green]Disponible[/]"
                : $"[red]{Markup.Escape(descriptor.Availability.Detail ?? "No disponible")}[/]";

            table.AddRow(
                Markup.Escape(descriptor.DisplayName),
                Markup.Escape(descriptor.Configuration.Type ?? "-"),
                Markup.Escape(tasks),
                Markup.Escape(languages),
                status);
        }

        AnsiConsole.Write(table);
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
        table.AddRow("/reset", "Limpiar el contexto actual y crear una nueva sesión");
        table.AddRow("/history", "Mostrar el historial de mensajes");
        table.AddRow("/config", "Ver o actualizar parámetros");
        table.AddRow("/save <id>", "Crear un snapshot de la sesión actual");
        table.AddRow("/load <id>", "Cargar una sesión guardada");
        table.AddRow("/sessions", "Listar sesiones disponibles");
        table.AddRow("/tools", "Mostrar herramientas MCP configuradas");

        table.AddEmptyRow();
        table.AddRow(
            "(texto libre)",
            "Cualquier mensaje que escribas se ejecutará como objetivo del agente.");

        AnsiConsole.MarkupLine("[bold cyan]Comandos disponibles:[/]");
        AnsiConsole.Write(table);
    }

    private async Task HandleAgentAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (!_configurationService.Current.Agent.Enabled)
        {
            AnsiConsole.MarkupLine("[yellow]El agente está deshabilitado en la configuración actual.[/]");
            return;
        }

        if (arguments.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Debes indicar una tarea para el agente.[/]");
            return;
        }

        var objective = string.Join(' ', arguments);
        AgentExecutionResult? result = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("[cyan]Ejecutando agente...[/]", async ctx =>
            {
                ctx.Status("Solicitando al modelo...");
                result = await _agentService.ExecuteTaskAsync(objective, cancellationToken).ConfigureAwait(false);
            });

        if (result is null)
        {
            AnsiConsole.MarkupLine("[red]El agente no devolvió ningún resultado.[/]");
            return;
        }

        RenderAgentResult(result);
        _chatService.RegisterAgentToolInvocations(result.ToolInvocations);
    }

    private static void RenderAgentResult(AgentExecutionResult result)
    {
        if (result.ToolInvocations.Count > 0)
        {
            var table = new Table().Border(TableBorder.Rounded).Title("Ejecución de herramientas");
            table.AddColumn("Herramienta");
            table.AddColumn("Estado");
            table.AddColumn("Salida (previa)");

            foreach (var invocation in result.ToolInvocations)
            {
                var status = invocation.IsError ? "[red]Error[/]" : "[green]OK[/]";
                var preview = invocation.Output.Length > 240
                    ? invocation.Output[..240] + "…"
                    : invocation.Output;
                table.AddRow(
                    Markup.Escape(invocation.ToolName),
                    status,
                    Markup.Escape(preview));
            }

            AnsiConsole.Write(table);
        }

        var statusText = result.Completed ? "completado" : "incompleto";
        var statusColor = result.Completed ? "green" : "red";
        AnsiConsole.MarkupLine($"[bold yellow]Agent[/] [{statusColor}]{statusText}[/]");

        var responsePanel = new Panel(Markup.Escape(result.FinalResponse))
        {
            Header = new PanelHeader("Respuesta final", Justify.Center),
            Border = BoxBorder.Rounded,
        };

        AnsiConsole.Write(responsePanel);
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
                    case "agent.model":
                        config.Agent.Model = string.IsNullOrWhiteSpace(value) ? null : value;
                        break;
                    case "agent.enabled":
                        if (bool.TryParse(value, out var agentEnabled))
                        {
                            config.Agent.Enabled = agentEnabled;
                        }

                        break;
                    case "agent.max_iterations":
                        if (int.TryParse(value, out var iterations) && iterations > 0)
                        {
                            config.Agent.MaxIterations = iterations;
                        }

                        break;
                    case "agent.allow_file_writes":
                        if (bool.TryParse(value, out var allowWrites))
                        {
                            config.Agent.AllowFileWrites = allowWrites;
                        }

                        break;
                    case "agent.working_directory":
                        config.Agent.WorkingDirectory = string.IsNullOrWhiteSpace(value) ? null : value;
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
        table.AddRow("Agent.Enabled", configuration.Agent.Enabled.ToString());
        table.AddRow("Agent.Model", configuration.Agent.Model ?? "(hereda Chat.Model)");
        table.AddRow("Agent.MaxIterations", configuration.Agent.MaxIterations.ToString());
        table.AddRow("Agent.AllowFileWrites", configuration.Agent.AllowFileWrites.ToString());
        table.AddRow(
            "Agent.WorkingDirectory",
            string.IsNullOrWhiteSpace(configuration.Agent.WorkingDirectory)
                ? "."
                : configuration.Agent.WorkingDirectory);

        AnsiConsole.MarkupLine("[bold cyan]Configuración actual:[/]");
        AnsiConsole.Write(table);
    }

    private async Task HandleSaveAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Debes proporcionar un nombre de sesión.[/]");
            return;
        }

        var sessionName = arguments[0];
        var normalized = await _chatService.SaveSnapshotAsync(sessionName, cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine($"[green]Snapshot creado como '{Markup.Escape(normalized)}'.[/]");
    }

    private async Task HandleLoadAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Debes indicar el nombre de la sesión a cargar.[/]");
            return;
        }

        var sessionName = arguments[0];
        var loaded = await _chatService.LoadSessionAsync(sessionName, cancellationToken).ConfigureAwait(false);
        if (!loaded)
        {
            AnsiConsole.MarkupLine("[yellow]La sesión no existe o no pudo cargarse.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Sesión '{Markup.Escape(sessionName)}' cargada.[/]");
        RenderSessionInfo();
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
        table.AddColumn("ID");
        table.AddColumn("Creada");
        table.AddColumn("Actualizada");
        table.AddColumn("Mensajes");
        table.AddColumn("Herramientas");
        foreach (var session in sessions)
        {
            table.AddRow(
                Markup.Escape(session.Id),
                session.CreatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
                session.UpdatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
                session.MessageCount.ToString(CultureInfo.InvariantCulture),
                session.ToolCount.ToString(CultureInfo.InvariantCulture));
        }

        AnsiConsole.Write(table);
    }

    private void RenderSessionInfo()
    {
        var session = _chatService.CurrentSession;
        var created = session.CreatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        var updated = session.UpdatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        var messageCount = session.Messages.Count;
        var toolCount = session.ToolsUsed.Count;

        AnsiConsole.MarkupLine(
            $"[grey]Sesión activa: {Markup.Escape(session.Id)} — creada {created}, última actualización {updated}. Mensajes: {messageCount}, herramientas: {toolCount}[/]");
    }

    private static void RenderWelcome()
    {
        var rule = new Rule("[bold magenta]RapidCLI Assistant[/]")
        {
            Justification = Justify.Center,
        };
        AnsiConsole.Write(rule);
        AnsiConsole.MarkupLine("[grey]Describe la tarea y el agente actuará sobre el repositorio actual. Usa [u]/help[/] para ver comandos disponibles.[/]");
    }
}
