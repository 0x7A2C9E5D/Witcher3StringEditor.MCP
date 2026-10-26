using System.ComponentModel;
using JetBrains.Annotations;
using ModelContextProtocol.Server;
using Witcher3StringEditor.MCP.Models;
using Witcher3StringEditor.MCP.Storage;
using Witcher3StringEditor.Serializers;
using W3StringsDocument = Witcher3StringEditor.MCP.Models.W3StringsDocument;

namespace Witcher3StringEditor.MCP.Tools;

/// <summary>
///     MCP tools that manage W3Strings documents held in memory by the server.
///     Every document is created by open/create, which returns the handle all other tools work with.
/// </summary>
[UsedImplicitly]
internal sealed class W3StringsDocumentTools
{
    [McpServerTool(Name = "open_document", ReadOnly = true)]
    [Description("Opens an existing .w3strings file and returns a document handle used by the document tools.")]
    public static Task<Guid> OpenDocument(
        [Description("Path to the .w3strings file to open.")]
        string filePath)
    {
        return W3StringsDocument.OpenAsync(filePath);
    }

    [McpServerTool(Name = "create_document")]
    [Description(
        "Creates a new empty document held in memory and returns a document handle. The version and language are chosen by save_document when the document is written to disk.")]
    public static Task<Guid> CreateDocument()
    {
        return W3StringsDocument.CreateAsync();
    }

    [McpServerTool(Name = "count", ReadOnly = true)]
    [Description("Returns the number of string items in the document.")]
    public static Task<int> Count(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle)
    {
        return W3StringsDocument.ExecuteAsync(handle, document => document.Items.Count);
    }

    [McpServerTool(Name = "is_dirty", ReadOnly = true)]
    [Description(
        "Returns whether the document has unsaved changes: true after any item change or a new document, false after save_document.")]
    public static Task<bool> IsDirty(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle)
    {
        return W3StringsDocument.ExecuteAsync(handle, document => document.IsDirty);
    }

    [McpServerTool(Name = "navigate", ReadOnly = true)]
    [Description(
        "Moves relative to an item and returns the item reached: offset 0 returns the reference item, positive offsets move forward, negative offsets move backward. Omit strId to start from the first item. Returns nothing when the target is outside the document.")]
    public static Task<StringItem?> Navigate(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle,
        [Description("Number of items to move. 0 returns the reference item, positive moves forward, negative moves backward.")]
        int offset,
        [Description(
            "32-bit unsigned decimal string id of the reference item. Omit it to start from the first item.")]
        string? strId = null)
    {
        return W3StringsDocument.ExecuteAsync(handle, document => document.Navigate(strId, offset));
    }

    [McpServerTool(Name = "get_item", ReadOnly = true)]
    [Description("Returns the string item with the given 32-bit unsigned string id.")]
    public static Task<StringItem> GetItem(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle,
        [Description("32-bit unsigned string id of the item, in decimal.")]
        string strId)
    {
        return W3StringsDocument.ExecuteAsync(handle, document => document.GetItem(strId));
    }

    [McpServerTool(Name = "search_items", ReadOnly = true)]
    [Description(
        "Searches the string items of the document by substring in their string id, text or key name and returns all matches.")]
    public static Task<W3StringsSearchResult> SearchItems(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle,
        [Description("Substring to search for. It is matched against the string id, text and key name of every item.")]
        string query,
        [Description("Match case sensitively. Defaults to false.")]
        bool caseSensitive = false)
    {
        return W3StringsDocument.ExecuteAsync(handle,
            document => document.SearchItems(query, caseSensitive));
    }

    [McpServerTool(Name = "add_item")]
    [Description("Adds a string item to the document without writing to disk.")]
    public static Task AddItem(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle,
        [Description(
            "Item to add. strId is a 32-bit unsigned decimal id; text is the string; keyName and keyHex are optional.")]
        StringItem item)
    {
        return W3StringsDocument.ExecuteAsync(handle, document => document.AddItem(item));
    }

    [McpServerTool(Name = "update_item")]
    [Description(
        "Updates the text or key of an existing string item without writing to disk. Omit an argument to keep its value, pass an empty string to clear it.")]
    public static Task UpdateItem(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle,
        [Description("32-bit unsigned decimal string id of the item to update.")]
        string strId,
        [Description("New text. Omit to keep the current text, pass an empty string to clear it.")]
        string? text = null,
        [Description("New readable key name. Omit to keep the current name, pass an empty string to clear it.")]
        string? keyName = null,
        [Description(
            "New 32-bit hexadecimal key hash. Omit to keep the current hash, pass an empty string to clear it.")]
        string? keyHex = null)
    {
        return W3StringsDocument.ExecuteAsync(handle, document => document.UpdateItem(strId, text, keyName, keyHex));
    }

    [McpServerTool(Name = "update_item_id")]
    [Description("Changes the 32-bit unsigned string id of an item without writing to disk.")]
    public static Task UpdateItemId(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle,
        [Description("Current 32-bit unsigned decimal string id of the item.")]
        string strId,
        [Description("New 32-bit unsigned decimal string id. It must not be used by another item.")]
        string newStrId)
    {
        return W3StringsDocument.ExecuteAsync(handle, document => document.UpdateItemId(strId, newStrId));
    }

    [McpServerTool(Name = "update_item_key_name")]
    [Description(
        "Changes the readable key name of an item and clears its key hash, which is recalculated when the document is saved.")]
    public static Task UpdateItemKeyName(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle,
        [Description("32-bit unsigned decimal string id of the item.")]
        string strId,
        [Description("New readable key name. Pass an empty string to clear the name.")]
        string keyName)
    {
        return W3StringsDocument.ExecuteAsync(handle, document => document.UpdateItemKeyName(strId, keyName));
    }

    [McpServerTool(Name = "delete_item")]
    [Description("Removes a string item from the document without writing to disk.")]
    public static Task DeleteItem(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle,
        [Description("32-bit unsigned decimal string id of the item to delete.")]
        string strId)
    {
        return W3StringsDocument.ExecuteAsync(handle, document => document.DeleteItem(strId));
    }

    [McpServerTool(Name = "save_document", Destructive = true)]
    [Description(
        "Writes the document to disk, overwriting the target file, and returns true when it was written. The version is always required; the path is required for a document that was never saved; language and version are the values written to the file.")]
    public static Task<bool> SaveDocument(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle,
        [Description(
            "W3Strings container version, which decides the encoding: 162 or 163 use UTF-16LE (classic and next-gen editions), 164 uses UTF-8 (remastered edition). Versions are tied to the game edition and must not be mixed; 162 and 163 are written as 162.")]
        string version,
        [Description(
            "Path to write to. Defaults to the path the document was opened from or last saved to; required when the document has no path yet.")]
        string? filePath = null,
        [Description(
            "Language code or culture code to write the document for, for example en, cn or zh-Hans. Defaults to en.")]
        string language = "en")
    {
        return W3StringsDocument.SaveAsync(handle, filePath, language, version);
    }

    [McpServerTool(Name = "close_document")]
    [Description(
        "Closes the document and releases the handle. Unsaved changes are discarded unless saveBeforeClose is true; saving before close uses the language and version already stored in the file.")]
    public static Task CloseDocument(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle,
        [Description("Save pending changes before closing. Defaults to false.")]
        bool saveBeforeClose = false)
    {
        return W3StringsDocument.CloseAsync(handle, saveBeforeClose);
    }

    [McpServerTool(Name = "list_languages", ReadOnly = true)]
    [Description(
        "Lists the language codes a .w3strings container can be written for. Pass one to save_document as language.")]
    public static string[] ListLanguages()
    {
        return [.. W3Language.All.Select(language => language.Code)];
    }
}