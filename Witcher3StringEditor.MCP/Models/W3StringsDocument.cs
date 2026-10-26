using Witcher3StringEditor.Serializers;

namespace Witcher3StringEditor.MCP.Models;

/// <summary>
///     A W3Strings document held in memory by the MCP server: its handle, items and pending-changes flag.
///     The path, language and encoding only matter when the document is saved.
/// </summary>
internal sealed class W3StringsDocument
{
    public required Guid Handle { get; init; }

    public List<StringItem> Items { get; init; } = [];

    public bool IsDirty { get; set; }
}