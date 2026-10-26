using Witcher3StringEditor.Serializers;

namespace Witcher3StringEditor.MCP.Models;

/// <summary>
///     One match of a document search together with the matches next to it.
/// </summary>
internal sealed record W3StringsSearchResult(
    Guid Handle,
    string Query,
    bool IsCaseSensitive,
    int MatchCount,
    StringItem[]? Items);