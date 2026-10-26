namespace Witcher3StringEditor.MCP.Models;

/// <summary>
///     Summary of an open document.
/// </summary>
internal sealed record W3StringsDocumentInfo(
    Guid Handle,
    int ItemCount,
    bool IsDirty);