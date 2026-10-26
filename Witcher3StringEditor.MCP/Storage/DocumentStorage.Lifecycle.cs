using System.Text;
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
        public static async Task<Guid> OpenAsync(string filePath)
        {
            var fullPath = Path.GetFullPath(filePath);

            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (HandlesByPath.TryGetValue(fullPath, out var opened)) return opened;

                var document = await LoadAsync(fullPath).ConfigureAwait(false);

                Documents[document.Handle] = document;
                HandlesByPath[fullPath] = document.Handle;
                PathsByHandle[document.Handle] = fullPath;
                return document.Handle;
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>
        ///     Creates an empty document that is only held in memory until it is saved. The version and language
        ///     are chosen when the document is saved.
        /// </summary>
        public static async Task<Guid> CreateAsync()
        {
            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var document = new W3StringsDocument
                {
                    Handle = Guid.NewGuid(),
                    IsDirty = true
                };

                Documents[document.Handle] = document;
                return document.Handle;
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>
        ///     Writes the document to disk, optionally to a different path, with the given language and container
        ///     version. Versions 162 and 163 write UTF-16LE, version 164 writes UTF-8; a version is always
        ///     required because the document does not keep one.
        /// </summary>
        public static async Task SaveAsync(
            Guid handle, string? filePath, string languageCode, string version)
        {
            var language = ResolveLanguage(languageCode);
            var encoding = ResolveVersion(version);

            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var document = Find(handle);
                await SaveCoreAsync(document, filePath, language, encoding).ConfigureAwait(false);
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

                if (saveBeforeClose && document.IsDirty)
                {
                    if (!PathsByHandle.TryGetValue(document.Handle, out var path))
                        throw new ArgumentException(
                            "The document has never been saved, so there is no file to save before closing.",
                            nameof(saveBeforeClose));

                    var existing = await W3StringsSerializer.Deserialize(path).ConfigureAwait(false);

                    await SaveCoreAsync(
                        document, path, existing.Language ?? W3Language.Default, existing.Encoding)
                        .ConfigureAwait(false);
                }

                Documents.Remove(document.Handle);
                if (PathsByHandle.Remove(document.Handle, out var closedPath)) HandlesByPath.Remove(closedPath);
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
    private static async Task SaveCoreAsync(
        W3StringsDocument document, string? filePath, W3Language language, Encoding encoding)
    {
        var targetPath = string.IsNullOrWhiteSpace(filePath)
            ? PathsByHandle.GetValueOrDefault(document.Handle)
            : Path.GetFullPath(filePath);

        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException(
                "filePath is required to save a document that has no path yet.", nameof(filePath));

        if (HandlesByPath.TryGetValue(targetPath, out var owner) && owner != document.Handle)
            throw new InvalidOperationException($"The path is already open as document {owner}: {targetPath}");

        await document.WriteAsync(targetPath, language, encoding).ConfigureAwait(false);

        if (PathsByHandle.TryGetValue(document.Handle, out var previousPath) &&
            !string.Equals(previousPath, targetPath, StringComparison.OrdinalIgnoreCase))
            HandlesByPath.Remove(previousPath);

        PathsByHandle[document.Handle] = targetPath;
        HandlesByPath[targetPath] = document.Handle;

        document.IsDirty = false;
    }
}
