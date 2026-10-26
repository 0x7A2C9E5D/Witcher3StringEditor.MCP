using System.Text;

namespace Witcher3StringEditor.MCP.Serializers;

/// <summary>
///     W3Strings serializer.
/// </summary>
public static class W3StringsSerializer
{
    /// <summary>
    ///     Deserialize W3Strings from file.
    /// </summary>
    /// <param name="filePath">Path to file.</param>
    /// <returns>The contents the file holds: its items, language and encoding.</returns>
    public static async Task<W3StringsFileContent> Deserialize(string filePath)
    {
        return await Task.Run(() => ReadContent(filePath)); // Decode the container off the calling thread
    }

    /// <summary>
    ///     Serialize W3Strings file contents to a stream.
    /// </summary>
    /// <param name="content">The contents to serialize, with the language and encoding to write them with.</param>
    /// <returns>Result of serialization.</returns>
    public static async Task<(bool, Stream?)> Serialize(W3StringsFileContent content)
    {
        try
        {
            // The language key decides how every id and text is obfuscated, so a document without one cannot
            // be written.
            if (content.Language is null) return (false, null);
            var version = ContainerVersion(content.Encoding);
            var container = W3StringsBuilder.Build(content.Items, content.Language.Key);
            if (container is null) return (false, null);
            await using var stream = new MemoryStream();
            await W3StringsStorage.Of(container, version).WriteAsync(stream);
            return (true, stream);
        }
        catch (Exception)
        {
            return (false, null);
        }
    }

    /// <summary>
    ///     Get W3Strings container version for given encoding.
    /// </summary>
    /// <param name="encoding">Encoding.</param>
    /// <returns>Container version.</returns>
    private static uint ContainerVersion(Encoding encoding)
    {
        return encoding.Equals(Encoding.UTF8) ? W3StringsStorage.FirstUtf8Version : W3StringsStorage.Utf16LeVersion;
    }

    /// <summary>
    ///     Read the contents a W3Strings file holds.
    /// </summary>
    /// <param name="filePath">Path to file.</param>
    /// <returns>The contents the file holds.</returns>
    private static W3StringsFileContent ReadContent(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var storage = W3StringsStorage.Read(stream);
        var container = storage.ToContainer(stream);
        return new W3StringsFileContent
        {
            Items = W3StringsItemReader.Read(container),
            Language = W3Language.FromKey(container.Key),
            Encoding = storage.Version >= W3StringsStorage.FirstUtf8Version ? Encoding.UTF8 : Encoding.Unicode
        };
    }
}