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
    public static Task<W3StringsDocumentInfo> OpenDocument(
        [Description("Path to the .w3strings file to open.")]
        string filePath)
    {
        return W3StringsDocument.OpenAsync(filePath);
    }

    [McpServerTool(Name = "create_document")]
    [Description(
        "Creates a new empty document held in memory and returns a document handle. The version fixes the encoding; pass a path and language to save_document when writing it to disk.")]
    public static Task<W3StringsDocumentInfo> CreateDocument(
        [Description(
            "W3Strings container version, which decides the encoding: 162 or 163 use UTF-16LE (classic and next-gen editions), 164 uses UTF-8 (remastered edition). Versions are tied to the game edition and must not be mixed; 162 and 163 are written as 162.")]
        string version)
    {
        return W3StringsDocument.CreateAsync(version);
    }

    [McpServerTool(Name = "get_info", ReadOnly = true)]
    [Description(
        "Returns the summary of an open document without any of its items: handle, item count and dirty flag.")]
    public static Task<W3StringsDocumentInfo> GetInfo(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle)
    {
        return W3StringsDocument.GetInfoAsync(handle);
    }

    [McpServerTool(Name = "list_items", ReadOnly = true)]
    [Description(
        "Returns one page of the string items of the document, starting at offset. Use itemCount to get the total and hasMore to check for another page.")]
    public static Task<W3StringsItemPage> ListItems(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle,
        [Description("Zero based index of the first item to return. Defaults to 0.")]
        int offset = 0,
        [Description("Maximum number of items to return. Defaults to 50.")]
        int limit = DocumentStorage.DefaultPageSize)
    {
        return W3StringsDocument.ExecuteAsync(handle, document => document.ListItems(offset, limit));
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
        "Searches the string items of the document by substring in their string id, text or key name and returns one page of matches. Use matchCount to get the total and hasMore to check for another page.")]
    public static Task<W3StringsSearchResult> SearchItems(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle,
        [Description("Substring to search for. It is matched against the string id, text and key name of every item.")]
        string query,
        [Description("Match case sensitively. Defaults to false.")]
        bool caseSensitive = false,
        [Description("Zero based index of the first match to return. Defaults to 0.")]
        int offset = 0,
        [Description("Maximum number of matches to return. Defaults to 50.")]
        int limit = DocumentStorage.DefaultPageSize)
    {
        return W3StringsDocument.ExecuteAsync(handle,
            document => document.SearchItems(query, caseSensitive, offset, limit));
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
        "Writes the document to disk, overwriting the target file. The path is required for a document that was created without one; language and version are the values written to the file.")]
    public static Task<W3StringsDocumentInfo> SaveDocument(
        [Description("Document handle returned by open_document or create_document.")]
        Guid handle,
        [Description(
            "Path to write to. Defaults to the path the document was opened from; required when the document was created.")]
        string? filePath = null,
        [Description(
            "Language code or culture code to write the document for, for example en, cn or zh-Hans. Defaults to en.")]
        string language = "en",
        [Description(
            "W3Strings container version, which decides the encoding: 162 or 163 use UTF-16LE (classic and next-gen editions), 164 uses UTF-8 (remastered edition). Defaults to the version the document was opened or created with; passing a different version converts the file.")]
        string? version = null)
    {
        return W3StringsDocument.SaveAsync(handle, filePath, language, version);
    }

    [McpServerTool(Name = "close_document")]
    [Description(
        "Closes the document and releases the handle. Unsaved changes are discarded unless saveBeforeClose is true.")]
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