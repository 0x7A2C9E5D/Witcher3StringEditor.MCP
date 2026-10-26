using System.Text;
using Witcher3StringEditor.MCP.Models;
using Witcher3StringEditor.MCP.Serializers;

namespace Witcher3StringEditor.MCP.Storage;

/// <summary>
///     The lifecycle of the documents held in memory: opening, creating, saving and closing them, together with
///     the file load and write steps those operations use.
/// </summary>
internal static partial class DocumentStorage
{
    extension(W3StringsDocumentSession)
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
                var document = new W3StringsDocumentSession
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
        public static async Task<bool> SaveAsync(
            Guid handle, string? filePath, string languageCode, string version)
        {
            var language = ResolveLanguage(languageCode);
            var encoding = ResolveVersion(version);

            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var document = Find(handle);
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
    }

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
    ///     Loads a .w3strings file into an in-memory document.
    /// </summary>
    private static async Task<W3StringsDocumentSession> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"W3Strings file not found: {filePath}", filePath);

        var loaded = await W3StringsSerializer.Deserialize(filePath).ConfigureAwait(false);

        return new W3StringsDocumentSession
        {
            Handle = Guid.NewGuid(),
            Items = [.. loaded.Items.Select(Clone)]
        };
    }

    /// <summary>
    ///     Writes the document to a .w3strings file with the given language and encoding.
    /// </summary>
    private static async Task WriteAsync(
        W3StringsDocumentSession document, string filePath, W3Language language, Encoding encoding)
    {
        var content = new W3StringsFileContent
        {
            Items = document.Items,
            Language = language,
            Encoding = encoding
        };

        var (succeeded, stream) = await W3StringsSerializer.Serialize(content).ConfigureAwait(false);

        if (!succeeded || stream is not MemoryStream serialized)
            throw new InvalidOperationException(
                "The document could not be serialized. Check that every string id is a unique 32-bit unsigned " +
                "integer and that every key hash is a 32-bit hexadecimal value.");

        await File.WriteAllBytesAsync(filePath, serialized.ToArray()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Writes a document to disk and reports whether it succeeded. The caller must hold the gate.
    /// </summary>
    private static async Task<bool> SaveCoreAsync(
        W3StringsDocumentSession document, string? filePath, W3Language language, Encoding encoding)
    {
        var targetPath = string.IsNullOrWhiteSpace(filePath)
            ? PathsByHandle.GetValueOrDefault(document.Handle)
            : Path.GetFullPath(filePath);

        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException(
                "filePath is required to save a document that has no path yet.", nameof(filePath));

        if (HandlesByPath.TryGetValue(targetPath, out var owner) && owner != document.Handle)
            throw new InvalidOperationException($"The path is already open as document {owner}: {targetPath}");

        await WriteAsync(document, targetPath, language, encoding).ConfigureAwait(false);

        if (PathsByHandle.TryGetValue(document.Handle, out var previousPath) &&
            !string.Equals(previousPath, targetPath, StringComparison.OrdinalIgnoreCase))
            HandlesByPath.Remove(previousPath);

        PathsByHandle[document.Handle] = targetPath;
        HandlesByPath[targetPath] = document.Handle;

        document.IsDirty = false;
        return true;
    }
}
