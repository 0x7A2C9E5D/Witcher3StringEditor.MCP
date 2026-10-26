using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using Serilog;
using Witcher3StringEditor.MCP.Models;
using Witcher3StringEditor.MCP.Tools;

var builder = Host.CreateApplicationBuilder(args);
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
builder.Logging.AddConsole(options => { options.LogToStandardErrorThreshold = LogLevel.Trace; });
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