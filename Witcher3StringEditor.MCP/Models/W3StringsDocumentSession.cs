using Witcher3StringEditor.MCP.Serializers;

namespace Witcher3StringEditor.MCP.Models;

/// <summary>
///     An open W3Strings document session: the handle the tools work with, the editable items and the pending-changes flag.
///     The path, language and encoding only matter when the document is saved.
/// </summary>
internal sealed class W3StringsDocumentSession
{
    public required Guid Handle { get; init; }

    public List<StringItem> Items { get; init; } = [];

    public bool IsDirty { get; set; }
}