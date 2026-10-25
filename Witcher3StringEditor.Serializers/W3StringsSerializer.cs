using System.Text;
using Serilog;

namespace Witcher3StringEditor.Serializers;

/// <summary>
///     W3Strings serializer.
/// </summary>
public class W3StringsSerializer
{
    /// <summary>
    ///     Deserialize W3Strings from file.
    /// </summary>
    /// <param name="filePath">Path to file.</param>
    /// <returns>W3Strings items.</returns>
    public async Task<IReadOnlyList<StringItem>> Deserialize(string filePath)
    {
        return await Task.Run(() => ReadItems(filePath)); // Decode the container off the calling thread
    }

    /// <summary>
    ///     Serialize W3Strings to file.
    /// </summary>
    /// <param name="w3StringItems">W3Strings items.</param>
    /// <param name="language">Language to use.</param>
    /// <param name="encoding">Encoding to use.</param>
    /// <returns>Result of serialization.</returns>
    public async Task<(bool, Stream?)> Serialize(IReadOnlyList<StringItem> w3StringItems, W3Language language,
        Encoding encoding)
    {
        try
        {
            var version = ContainerVersion(encoding);
            var container = W3StringsBuilder.Build(w3StringItems, language.Key);
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
    ///     Read W3Strings items from file.
    /// </summary>
    /// <param name="filePath">Path to file.</param>
    /// <returns>W3Strings items.</returns>
    private static List<StringItem> ReadItems(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var storage = W3StringsStorage.Read(stream);
        var container = storage.ToContainer(stream);
        Log.Information("Read W3Strings v{Version} container (magic {Magic}) from {Path}", storage.Version,
            container.Magic == 0 ? "none" : $"0x{container.Magic:X8}",
            filePath);
        return [.. W3StringsItemReader.Read(container)];
    }
}