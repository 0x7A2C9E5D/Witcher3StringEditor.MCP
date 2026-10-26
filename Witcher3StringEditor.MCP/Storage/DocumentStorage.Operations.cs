using System.Text;
using Witcher3StringEditor.MCP.Models;
using Witcher3StringEditor.Serializers;
using W3StringsDocument = Witcher3StringEditor.MCP.Models.W3StringsDocument;

namespace Witcher3StringEditor.MCP.Storage;

/// <summary>
///     Reads and changes a W3Strings document held in memory, exposed as extension members of
///     <see cref="W3StringsDocument" />.
/// </summary>
internal static partial class DocumentStorage
{
    extension(W3StringsDocument document)
    {
        /// <summary>
        ///     Returns the summary of the document without any of its items.
        /// </summary>
        private W3StringsDocumentInfo Info()
        {
            return new W3StringsDocumentInfo(document.Handle, document.Items.Count, document.IsDirty);
        }

        /// <summary>
        ///     Writes the document to a .w3strings file with the given language and encoding.
        /// </summary>
        private async Task WriteAsync(string filePath, W3Language language, Encoding encoding)
        {
            var serializedDocument = new Witcher3StringEditor.Serializers.W3StringsDocument
            {
                Items = document.Items,
                Language = language,
                Encoding = encoding
            };

            var (succeeded, stream) = await W3StringsSerializer.Serialize(serializedDocument).ConfigureAwait(false);

            if (!succeeded || stream is not MemoryStream serialized)
                throw new InvalidOperationException(
                    "The document could not be serialized. Check that every string id is a unique 32-bit unsigned " +
                    "integer and that every key hash is a 32-bit hexadecimal value.");

            await File.WriteAllBytesAsync(filePath, serialized.ToArray()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Moves relative to an item and returns the item reached: offset 0 returns the reference item,
        ///     a positive offset moves forward and a negative one moves backward. An empty string id starts from
        ///     the first item; a target outside the document returns null.
        /// </summary>
        public StringItem? Navigate(string? strId, int offset)
        {
            if (document.Items.Count == 0) return null;

            var index = string.IsNullOrWhiteSpace(strId)
                ? 0
                : document.Items.IndexOf(FindItem(document, NormalizeStrId(strId)));
            var target = index + offset;

            return target >= 0 && target < document.Items.Count ? Clone(document.Items[target]) : null;
        }

        /// <summary>
        ///     Returns a copy of the item with the given string id.
        /// </summary>
        public StringItem GetItem(string strId)
        {
            var id = NormalizeStrId(strId);
            return Clone(FindItem(document, id));
        }

        /// <summary>
        ///     Searches the items of the document for the given text in their string id, text or key name and
        ///     returns every match.
        /// </summary>
        public W3StringsSearchResult SearchItems(string query, bool caseSensitive)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("The search query must not be empty.", nameof(query));

            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            var matches = document.Items
                .Where(item =>
                    item.StrId.Contains(query, comparison) ||
                    item.Text.Contains(query, comparison) ||
                    item.KeyName.Contains(query, comparison))
                .Select(Clone)
                .ToArray();

            return new W3StringsSearchResult(document.Handle, query, caseSensitive, matches.Length, matches);
        }

        /// <summary>
        ///     Adds an item to the document.
        /// </summary>
        public void AddItem(StringItem item)
        {
            var id = NormalizeStrId(item.StrId);

            if (document.Items.Any(existing => existing.StrId == id))
                throw new InvalidOperationException($"A string item with id {id} already exists.");

            document.Items.Add(new StringItem
            {
                StrId = id,
                KeyHex = NormalizeKeyHex(item.KeyHex),
                KeyName = item.KeyName,
                Text = item.Text
            });

            document.IsDirty = true;
        }

        /// <summary>
        ///     Updates the text or the key of the item with the given string id.
        /// </summary>
        public void UpdateItem(string strId, string? text, string? keyName, string? keyHex)
        {
            if (text is null && keyName is null && keyHex is null)
                throw new ArgumentException("At least one of text, keyName or keyHex must be provided.");

            var id = NormalizeStrId(strId);
            var normalizedKeyHex = keyHex is null ? null : NormalizeKeyHex(keyHex);
            var item = FindItem(document, id);
            var changed = false;

            if (text is not null && item.Text != text)
            {
                item.Text = text;
                changed = true;
            }

            if (keyName is not null && item.KeyName != keyName)
            {
                item.KeyName = keyName;
                // The old hash no longer belongs to the name, so it is cleared and recalculated on save.
                item.KeyHex = string.Empty;
                changed = true;
            }

            if (normalizedKeyHex is not null && item.KeyHex != normalizedKeyHex)
            {
                item.KeyHex = normalizedKeyHex;
                changed = true;
            }

            if (changed) document.IsDirty = true;
        }

        /// <summary>
        ///     Changes the string id of an item, keeping its text and key.
        /// </summary>
        public void UpdateItemId(string strId, string newStrId)
        {
            var oldId = NormalizeStrId(strId);
            var newId = NormalizeStrId(newStrId);
            var item = FindItem(document, oldId);

            if (oldId == newId) return;
            if (document.Items.Any(existing => existing.StrId == newId))
                throw new InvalidOperationException($"A string item with id {newId} already exists.");

            item.StrId = newId;
            document.IsDirty = true;
        }

        /// <summary>
        ///     Changes the readable key name of an item and clears the key hash the old name produced.
        /// </summary>
        public void UpdateItemKeyName(string strId, string? keyName)
        {
            var id = NormalizeStrId(strId);
            var name = keyName ?? string.Empty;
            var item = FindItem(document, id);

            if (item.KeyName == name) return;
            item.KeyName = name;
            // The old hash no longer belongs to the name, so it is cleared and recalculated on save.
            item.KeyHex = string.Empty;
            document.IsDirty = true;
        }

        /// <summary>
        ///     Removes the item with the given string id from the document.
        /// </summary>
        public void DeleteItem(string strId)
        {
            var id = NormalizeStrId(strId);
            document.Items.Remove(FindItem(document, id));
            document.IsDirty = true;
        }
    }
}
