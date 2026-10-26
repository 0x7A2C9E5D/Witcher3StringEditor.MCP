using System.Text;
using Witcher3StringEditor.Serializers;

namespace Witcher3StringEditor.MCP.Models;

/// <summary>
///     A W3Strings document held in memory by the MCP server. Its path, language and encoding are set when it
///     is opened from or saved to a file.
/// </summary>
internal sealed class W3StringsDocument
{
    public required Guid Handle { get; init; }

    public string? FilePath { get; set; }

    public W3Language? Language { get; set; }

    public Encoding? Encoding { get; set; }

    public List<StringItem> Items { get; init; } = [];

    public bool Dirty { get; set; }
}