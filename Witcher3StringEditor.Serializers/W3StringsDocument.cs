using System.Text;
using JetBrains.Annotations;

namespace Witcher3StringEditor.Serializers;

/// <summary>
///     The contents of a .w3strings file as they are read from it: the string items, the language the
///     container was written for and the encoding its texts are stored in.
/// </summary>
[PublicAPI]
public sealed class W3StringsDocument
{
    /// <summary>
    ///     The string items of the file, in container order.
    /// </summary>
    public required IReadOnlyList<StringItem> Items { get; init; }

    /// <summary>
    ///     The language the container was written for, or null when its language key names no single
    ///     language.
    /// </summary>
    public W3Language? Language { get; init; }

    /// <summary>
    ///     The encoding the texts of the container are stored in.
    /// </summary>
    public required Encoding Encoding { get; init; }
}