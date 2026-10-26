using System.Text;
using Serilog;

namespace Witcher3StringEditor.Serializers;

/// <summary>
///     W3Strings serializer.
/// </summary>
public static class W3StringsSerializer
{
    /// <summary>
    ///     Deserialize W3Strings from file.
    /// </summary>
    /// <param name="filePath">Path to file.</param>
    /// <returns>The document the file holds: its items, language and encoding.</returns>
    public static async Task<W3StringsDocument> Deserialize(string filePath)
    {
        return await Task.Run(() => ReadDocument(filePath)); // Decode the container off the calling thread
    }

    /// <summary>
    ///     Serialize W3Strings document to a stream.
    /// </summary>
    /// <param name="document">The document to serialize, with the language and encoding to write it with.</param>
    /// <returns>Result of serialization.</returns>
    public static async Task<(bool, Stream?)> Serialize(W3StringsDocument document)
    {
        try
        {
            // The language key decides how every id and text is obfuscated, so a document without one cannot
            // be written.
            if (document.Language is null) return (false, null);
            var version = ContainerVersion(document.Encoding);
            var container = W3StringsBuilder.Build(document.Items, document.Language.Key);
            if (container is null) return (false, null);
            await using var stream = new MemoryStream();
            await W3StringsStorage.Of(container, version).WriteAsync(stream);
            return (true, stream);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while serializing W3Strings");
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
    ///     Read the document W3Strings file holds.
    /// </summary>
    /// <param name="filePath">Path to file.</param>
    /// <returns>The document the file holds.</returns>
    private static W3StringsDocument ReadDocument(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var storage = W3StringsStorage.Read(stream);
        var container = storage.ToContainer(stream);
        Log.Information("Read W3Strings v{Version} container (magic {Magic}) from {Path}", storage.Version,
            container.Magic == 0 ? "none" : $"0x{container.Magic:X8}",
            filePath);
        return new W3StringsDocument
        {
            Items = W3StringsItemReader.Read(container),
            Language = W3Language.FromKey(container.Key),
            Encoding = storage.Version >= W3StringsStorage.FirstUtf8Version ? Encoding.UTF8 : Encoding.Unicode
        };
    }
}