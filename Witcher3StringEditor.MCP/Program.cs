using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using Serilog;
using Serilog.Events;
using Witcher3StringEditor.MCP.Models;
using Witcher3StringEditor.MCP.Tools;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
    .WriteTo.File("log.txt", rollOnFileSizeLimit: true)
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

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