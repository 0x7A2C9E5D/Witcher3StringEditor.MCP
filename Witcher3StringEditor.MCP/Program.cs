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

// Standard output carries the MCP protocol, so every log entry goes to standard error.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("ModelContextProtocol", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Route the host's Microsoft.Extensions.Logging output into Serilog.
    builder.Logging.ClearProviders();
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
