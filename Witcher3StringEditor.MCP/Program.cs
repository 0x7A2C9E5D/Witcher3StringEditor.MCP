using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using Witcher3StringEditor.MCP.Models;
using Witcher3StringEditor.MCP.Tools;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Combine the source generated contracts for the W3Strings tool results with the contracts the SDK
// already knows, so tool results serialize without reflection (the server is published with Native AOT).
var jsonOptions = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
{
    TypeInfoResolver = JsonTypeInfoResolver.Combine(
        W3StringsJsonContext.Default,
        McpJsonUtilities.DefaultOptions.TypeInfoResolver!)
};

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<W3StringsDocumentTools>(jsonOptions);

await builder.Build().RunAsync();