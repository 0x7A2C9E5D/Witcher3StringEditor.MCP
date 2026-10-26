using System.Globalization;
using System.Text;
using Witcher3StringEditor.Serializers;
using W3StringsDocument = Witcher3StringEditor.MCP.Models.W3StringsDocument;

namespace Witcher3StringEditor.MCP.Storage;

/// <summary>
///     Owns the W3Strings documents held in memory: hands out handles, keeps the path mapping, serializes access
///     to the documents and exposes their lifecycle and item operations as extension members of
///     <see cref="W3StringsDocument" />.
/// </summary>
internal static partial class DocumentStorage
{
    internal const int DefaultPageSize = 50;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<Guid, W3StringsDocument> Documents = [];
    private static readonly Dictionary<string, Guid> HandlesByPath = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Guid, string> PathsByHandle = [];

    /// <summary>
    ///     Resolves the language of a .w3strings file from its user facing name.
    /// </summary>
    private static W3Language ResolveLanguage(string? code)
    {
        var language = W3Language.FromCode(code ?? "en");
        return language ?? throw new ArgumentException(
            $"Unknown language code: {code}. Supported codes: " +
            $"{string.Join(", ", W3Language.All.Select(item => item.Code))}.",
            nameof(code));
    }

    /// <summary>
    ///     Resolves the encoding from the W3Strings container version. Versions 162 and 163 are the UTF-16LE
    ///     generation used by the classic and next-gen editions (both are written as 162); version 164 is the
    ///     UTF-8 generation used by the remastered edition. A version is required because the document itself
    ///     does not keep one.
    /// </summary>
    private static Encoding ResolveVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException(
                "A version is required to save a document: 162 or 163 (UTF-16LE), 164 (UTF-8).", nameof(version));

        return version.Trim().ToLowerInvariant() switch
        {
            "162" or "v162" or "163" or "v163" => Encoding.Unicode,
            "164" or "v164" => Encoding.UTF8,
            _ => throw new ArgumentException(
                $"Unknown version: {version}. Supported versions: 162 and 163 (UTF-16LE), 164 (UTF-8).",
                nameof(version))
        };
    }

    /// <summary>
    ///     Gets an open document or throws when the handle is unknown.
    /// </summary>
    private static W3StringsDocument Find(Guid handle)
    {
        return Documents.TryGetValue(handle, out var document)
            ? document
            : throw new KeyNotFoundException($"Unknown document handle: {handle}");
    }

    /// <summary>
    ///     Validates the paging arguments before the items are read.
    /// </summary>
    private static void ValidatePage(int offset, int limit)
    {
        if (offset < 0) throw new ArgumentException("offset must not be negative.", nameof(offset));

        if (limit < 1) throw new ArgumentException("limit must be at least 1.", nameof(limit));
    }

    /// <summary>
    ///     Finds an item by its already normalized string id.
    /// </summary>
    private static StringItem FindItem(W3StringsDocument document, string id)
    {
        return document.Items.FirstOrDefault(item => item.StrId == id)
               ?? throw new KeyNotFoundException($"A string item with id {id} does not exist.");
    }

    /// <summary>
    ///     Parses a string id into its canonical 32-bit unsigned decimal form, so that values such as
    ///     "01" and "1" cannot be treated as two different items.
    /// </summary>
    private static string NormalizeStrId(string? strId)
    {
        return uint.TryParse(strId, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id.ToString(CultureInfo.InvariantCulture)
            : throw new ArgumentException($"String id '{strId}' is not a 32-bit unsigned integer.", nameof(strId));
    }

    /// <summary>
    ///     Normalizes a key hash to its 8 digit uppercase hexadecimal form, or to an empty string when cleared.
    /// </summary>
    private static string NormalizeKeyHex(string? keyHex)
    {
        if (string.IsNullOrWhiteSpace(keyHex)) return string.Empty;

        return uint.TryParse(keyHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hash)
            ? hash.ToString("X8", CultureInfo.InvariantCulture)
            : throw new ArgumentException($"Key hash '{keyHex}' is not a 32-bit hexadecimal value.", nameof(keyHex));
    }

    private static StringItem Clone(StringItem item)
    {
        return new StringItem
        {
            StrId = item.StrId,
            KeyHex = item.KeyHex,
            KeyName = item.KeyName,
            Text = item.Text
        };
    }
}
