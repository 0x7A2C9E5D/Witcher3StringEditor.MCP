using System.Text;
using Witcher3StringEditor.MCP.Models;
using Witcher3StringEditor.Serializers;
using W3StringsDocument = Witcher3StringEditor.MCP.Models.W3StringsDocument;

namespace Witcher3StringEditor.MCP.Storage;

/// <summary>
///     The lifecycle of the documents held in memory: opening, creating, saving, closing and running operations
///     under the serialization gate.
/// </summary>
internal static partial class DocumentStorage
{
    extension(W3StringsDocument)
    {
        /// <summary>
        ///     Opens an existing W3Strings file, or returns the handle of the already open document for that path.
        /// </summary>
        public static async Task<W3StringsDocumentInfo> OpenAsync(string filePath)
        {
            var fullPath = Path.GetFullPath(filePath);

            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (HandlesByPath.TryGetValue(fullPath, out var opened)) return Documents[opened].Info();

                var document = await LoadAsync(fullPath, NextHandle()).ConfigureAwait(false);

                Documents[document.Handle] = document;
                HandlesByPath[fullPath] = document.Handle;
                return document.Info();
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>
        ///     Creates an empty document that is only held in memory until it is saved.
        /// </summary>
        public static async Task<W3StringsDocumentInfo> CreateAsync()
        {
            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var document = new W3StringsDocument
                {
                    Handle = NextHandle(),
                    Dirty = true
                };

                Documents[document.Handle] = document;
                return document.Info();
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>
        ///     Returns the summary of the document without any of its items.
        /// </summary>
        public static Task<W3StringsDocumentInfo> GetInfoAsync(string handle)
        {
            return ExecuteAsync(handle, document => document.Info());
        }

        /// <summary>
        ///     Writes the document to disk, optionally to a different path, with the given language and encoding.
        /// </summary>
        public static async Task<W3StringsDocumentInfo> SaveAsync(
            string handle, string? filePath, string languageCode, string encodingName)
        {
            var (language, encoding) = Resolve(languageCode, encodingName);

            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await SaveCoreAsync(Find(handle), filePath, language, encoding).ConfigureAwait(false);
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>
        ///     Closes the document and drops it from memory, optionally saving pending changes first.
        /// </summary>
        public static async Task CloseAsync(string handle, bool saveBeforeClose)
        {
            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var document = Find(handle);

                if (saveBeforeClose && document.Dirty)
                {
                    await SaveCoreAsync(
                        document,
                        null,
                        document.Language ?? W3Language.Default,
                        document.Encoding ?? Encoding.UTF8).ConfigureAwait(false);
                }

                Documents.Remove(document.Handle);
                if (document.FilePath is not null) HandlesByPath.Remove(document.FilePath);
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>
        ///     Runs a read operation against an open document while holding the gate.
        /// </summary>
        public static Task<T> ExecuteAsync<T>(string handle, Func<W3StringsDocument, T> operation)
        {
            return ExecuteCoreAsync(handle, operation);
        }

        /// <summary>
        ///     Runs a write operation against an open document while holding the gate.
        /// </summary>
        public static Task ExecuteAsync(string handle, Action<W3StringsDocument> operation)
        {
            return ExecuteCoreAsync(handle, operation);
        }
    }

    /// <summary>
    ///     Loads a .w3strings file into an in-memory document.
    /// </summary>
    private static async Task<W3StringsDocument> LoadAsync(string filePath, string handle)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"W3Strings file not found: {filePath}", filePath);

        var loaded = await W3StringsSerializer.Deserialize(filePath).ConfigureAwait(false);

        return new W3StringsDocument
        {
            Handle = handle,
            FilePath = filePath,
            Language = loaded.Language,
            Encoding = loaded.Encoding,
            Items = [.. loaded.Items.Select(Clone)]
        };
    }

    private static async Task<T> ExecuteCoreAsync<T>(string handle, Func<W3StringsDocument, T> operation)
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

    private static async Task ExecuteCoreAsync(string handle, Action<W3StringsDocument> operation)
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

    /// <summary>
    ///     Writes a document to disk. The caller must hold the gate.
    /// </summary>
    private static async Task<W3StringsDocumentInfo> SaveCoreAsync(
        W3StringsDocument document, string? filePath, W3Language language, Encoding encoding)
    {
        var targetPath = string.IsNullOrWhiteSpace(filePath) ? document.FilePath : Path.GetFullPath(filePath);

        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException(
                "filePath is required to save a document that was created without a path.", nameof(filePath));

        if (HandlesByPath.TryGetValue(targetPath, out var owner) && owner != document.Handle)
            throw new InvalidOperationException($"The path is already open as document {owner}: {targetPath}");

        await document.WriteAsync(targetPath, language, encoding).ConfigureAwait(false);

        if (document.FilePath is not null &&
            !string.Equals(document.FilePath, targetPath, StringComparison.OrdinalIgnoreCase))
            HandlesByPath.Remove(document.FilePath);

        document.FilePath = targetPath;
        document.Language = language;
        document.Encoding = encoding;
        HandlesByPath[targetPath] = document.Handle;

        document.Dirty = false;
        return document.Info();
    }
}
