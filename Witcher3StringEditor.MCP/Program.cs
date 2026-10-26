using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using Witcher3StringEditor.MCP.Models;
using Witcher3StringEditor.MCP.Tools;

var builder = Host.CreateApplicationBuilder(args);

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