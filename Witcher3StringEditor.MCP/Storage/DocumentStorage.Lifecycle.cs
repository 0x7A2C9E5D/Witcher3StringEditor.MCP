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

                var document = await LoadAsync(fullPath).ConfigureAwait(false);

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
        ///     Creates an empty document that is only held in memory until it is saved. The version decides the
        ///     encoding of the document: 162 and 163 are UTF-16LE, 164 is UTF-8.
        /// </summary>
        public static async Task<W3StringsDocumentInfo> CreateAsync(string version)
        {
            var encoding = ResolveVersion(version) ??
                           throw new ArgumentException(
                               "A version is required to create a document: 162 or 163 (UTF-16LE), 164 (UTF-8).",
                               nameof(version));

            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var document = new W3StringsDocument
                {
                    Handle = Guid.NewGuid(),
                    Encoding = encoding,
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
        public static Task<W3StringsDocumentInfo> GetInfoAsync(Guid handle)
        {
            return ExecuteAsync(handle, document => document.Info());
        }

        /// <summary>
        ///     Writes the document to disk, optionally to a different path, with the given language and container
        ///     version. Versions 162 and 163 write UTF-16LE, version 164 writes UTF-8; when no version is given the
        ///     version the document already uses is kept.
        /// </summary>
        public static async Task<W3StringsDocumentInfo> SaveAsync(
            Guid handle, string? filePath, string languageCode, string? version)
        {
            var language = ResolveLanguage(languageCode);

            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var document = Find(handle);
                var encoding = ResolveVersion(version) ?? document.Encoding ?? Encoding.UTF8;
                return await SaveCoreAsync(document, filePath, language, encoding).ConfigureAwait(false);
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>
        ///     Closes the document and drops it from memory, optionally saving pending changes first.
        /// </summary>
        public static async Task CloseAsync(Guid handle, bool saveBeforeClose)
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
        public static Task<T> ExecuteAsync<T>(Guid handle, Func<W3StringsDocument, T> operation)
        {
            return ExecuteCoreAsync(handle, operation);
        }

        /// <summary>
        ///     Runs a write operation against an open document while holding the gate.
        /// </summary>
        public static Task ExecuteAsync(Guid handle, Action<W3StringsDocument> operation)
        {
            return ExecuteCoreAsync(handle, operation);
        }
    }

    /// <summary>
    ///     Loads a .w3strings file into an in-memory document.
    /// </summary>
    private static async Task<W3StringsDocument> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"W3Strings file not found: {filePath}", filePath);

        var loaded = await W3StringsSerializer.Deserialize(filePath).ConfigureAwait(false);

        return new W3StringsDocument
        {
            Handle = Guid.NewGuid(),
            FilePath = filePath,
            Language = loaded.Language,
            Encoding = loaded.Encoding,
            Items = [.. loaded.Items.Select(Clone)]
        };
    }

    private static async Task<T> ExecuteCoreAsync<T>(Guid handle, Func<W3StringsDocument, T> operation)
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

    private static async Task ExecuteCoreAsync(Guid handle, Action<W3StringsDocument> operation)
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
