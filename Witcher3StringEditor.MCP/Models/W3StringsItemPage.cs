using Witcher3StringEditor.Serializers;

namespace Witcher3StringEditor.MCP.Models;

/// <summary>
///     One page of the items of a document.
/// </summary>
internal sealed record W3StringsItemPage(
    string Handle,
    int ItemCount,
    int Offset,
    int Limit,
    bool HasMore,
    StringItem[] Items);