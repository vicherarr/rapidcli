using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RapidCli.Application.Configurations;
using RapidCli.Application.Services;
using RapidCli.Cli;
using RapidCli.Infrastructure.Extensions;
using Spectre.Console;

var rootCommand = new RootCommand("RapidCLI - Asistente CLI impulsado por IA");
var configOption = new Option<FileInfo?>("--config", "Ruta a un archivo de configuración adicional");
rootCommand.AddOption(configOption);

rootCommand.SetHandler(async (InvocationContext context) =>
{
    var cancellationToken = context.GetCancellationToken();
    var config = context.ParseResult.GetValueForOption(configOption);
    var builder = Host.CreateApplicationBuilder();
    builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);

    if (config is not null)
    {
        if (config.Exists)
        {
            builder.Configuration.AddJsonFile(config.FullName, optional: false, reloadOnChange: true);
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]El archivo de configuración '{Markup.Escape(config.FullName)}' no existe.[/]");
        }
    }

    builder.Configuration.AddEnvironmentVariables(prefix: "RAPIDCLI_");

    builder.Services.AddLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });
        logging.SetMinimumLevel(LogLevel.Warning);
    });

    builder.Services.Configure<ChatConfiguration>(builder.Configuration.GetSection("Chat"));
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddSingleton<CliRunner>();

    using var host = builder.Build();
    var runner = host.Services.GetRequiredService<CliRunner>();
    await runner.RunAsync(cancellationToken).ConfigureAwait(false);
});

return await rootCommand.InvokeAsync(args);
