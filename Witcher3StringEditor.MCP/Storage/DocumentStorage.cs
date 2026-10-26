using System.Globalization;
using Witcher3StringEditor.MCP.Models;
using Witcher3StringEditor.MCP.Serializers;

namespace Witcher3StringEditor.MCP.Storage;

/// <summary>
///     Owns the documents held in memory: their handles, the path mapping, the serialization gate and the
///     lookups and helpers shared by the document lifecycle and the item operations.
/// </summary>
internal static partial class DocumentStorage
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<Guid, W3StringsDocumentSession> Documents = [];
    private static readonly Dictionary<string, Guid> HandlesByPath = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Guid, string> PathsByHandle = [];

    extension(W3StringsDocumentSession)
    {
        /// <summary>
        ///     Runs a read operation against an open document while holding the gate.
        /// </summary>
        public static Task<T> ExecuteAsync<T>(Guid handle, Func<W3StringsDocumentSession, T> operation)
        {
            return ExecuteCoreAsync(handle, operation);
        }

        /// <summary>
        ///     Runs a write operation against an open document while holding the gate.
        /// </summary>
        public static Task ExecuteAsync(Guid handle, Action<W3StringsDocumentSession> operation)
        {
            return ExecuteCoreAsync(handle, operation);
        }
    }

    /// <summary>
    ///     Gets an open document or throws when the handle is unknown.
    /// </summary>
    private static W3StringsDocumentSession Find(Guid handle)
    {
        return Documents.TryGetValue(handle, out var document)
            ? document
            : throw new KeyNotFoundException($"Unknown document handle: {handle}");
    }

    /// <summary>
    ///     Finds an item by its already normalized string id.
    /// </summary>
    private static StringItem FindItem(W3StringsDocumentSession document, string id)
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

    private static async Task<T> ExecuteCoreAsync<T>(Guid handle, Func<W3StringsDocumentSession, T> operation)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return operation(Find(handle));
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task ExecuteCoreAsync(Guid handle, Action<W3StringsDocumentSession> operation)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            operation(Find(handle));
        }
        finally
        {
            Gate.Release();
        }
    }
}
