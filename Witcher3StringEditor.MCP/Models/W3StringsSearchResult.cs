using Witcher3StringEditor.Serializers;

namespace Witcher3StringEditor.MCP.Models;

/// <summary>
///     Result of searching the items of a document, one page at a time.
/// </summary>
internal sealed record W3StringsSearchResult(
    Guid Handle,
    string Query,
    bool CaseSensitive,
    int MatchCount,
    int ItemCount,
    int Offset,
    int Limit,
    bool HasMore,
    StringItem[] Items);