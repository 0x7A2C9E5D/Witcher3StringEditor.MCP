using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using Serilog;
using Serilog.Events;
using Witcher3StringEditor.MCP.Models;
using Witcher3StringEditor.MCP.Tools;

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.AddConsole(consoleLogOptions =>
    {
        consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
    });
    
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
        .WriteTo.File("log.txt", rollOnFileSizeLimit: true)
        .CreateLogger();

    builder.Logging.AddSerilog();

    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<W3StringsDocumentTools>(new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                W3StringsJsonContext.Default,
                McpJsonUtilities.DefaultOptions.TypeInfoResolver!)
        });

    await builder.Build().RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "The MCP server terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}