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
    private static readonly Dictionary<string, W3StringsDocument> Documents = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> HandlesByPath = new(StringComparer.OrdinalIgnoreCase);
    private static long _nextHandle;

    /// <summary>
    ///     Resolves the language and encoding of a .w3strings file from their user facing names.
    /// </summary>
    private static (W3Language Language, Encoding Encoding) Resolve(string? languageCode, string? encodingName)
    {
        return (ResolveLanguage(languageCode), ResolveEncoding(encodingName));
    }

    private static W3Language ResolveLanguage(string? code)
    {
        var language = W3Language.FromCode(code ?? "en");
        return language ?? throw new ArgumentException(
            $"Unknown language code: {code}. Supported codes: " +
            $"{string.Join(", ", W3Language.All.Select(item => item.Code))}.",
            nameof(code));
    }

    private static Encoding ResolveEncoding(string? name)
    {
        return name?.Trim().ToLowerInvariant() switch
        {
            null or "" or "utf-8" or "utf8" => Encoding.UTF8,
            "utf-16le" or "utf16le" or "utf-16" or "utf16" or "unicode" => Encoding.Unicode,
            _ => throw new ArgumentException(
                $"Unknown encoding: {name}. Supported encodings: utf-8, utf-16le.",
                nameof(name))
        };
    }

    /// <summary>
    ///     Gets an open document or throws when the handle is unknown.
    /// </summary>
    private static W3StringsDocument Find(string handle)
    {
        return Documents.TryGetValue(handle, out var document)
            ? document
            : throw new KeyNotFoundException($"Unknown document handle: {handle}");
    }

    private static string NextHandle() => $"w3doc-{++_nextHandle}";

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
