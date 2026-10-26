using System.Text.Json.Serialization;
using Witcher3StringEditor.Serializers;

namespace Witcher3StringEditor.MCP.Models;

/// <summary>
///     Source generated JSON contract for the W3Strings tool results, so the tools keep working
///     under Native AOT without reflection based serialization.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(W3StringsSearchResult))]
[JsonSerializable(typeof(StringItem))]
[JsonSerializable(typeof(StringItem[]))]
internal sealed partial class W3StringsJsonContext : JsonSerializerContext;