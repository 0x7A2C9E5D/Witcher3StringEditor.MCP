---
name: w3strings-mcp
description: Install, connect, and drive the W3Strings MCP server for The Witcher 3 .w3strings localization files (15 stdio tools to open, search, edit, add/delete and save entries as version 162/163/164). Use for .w3strings inspection or editing, Witcher 3 text and mod translation, 巫师 3 文本/汉化/翻译/mods, or when a client must be connected to and debugged against the published server.
---

# W3Strings MCP: entry point

A local (stdio) MCP server that reads and edits The Witcher 3 `.w3strings` text containers.
It is published on NuGet.org and on the official MCP Registry as
`io.github.0x7A2C9E5D/Witcher3StringEditor.MCP`, version `0.1.0-beta.4`.

The one architectural fact that shapes every workflow: **each call works on an in-memory
document identified by a `Guid` handle, and only `save_document` touches the disk.** Edits are
staged in memory, so the original file is untouched until the user asks for a save.

## What the server does, and what it does not

| Can | Cannot |
| --- | --- |
| open, inspect, search (substring), edit text, add/delete entries | bulk edit in one call, regex search/replace |
| change a `strId` or a key hash, set a key name | import/export CSV or Excel, merge two files |
| save as version `162`, `163` or `164`, for 18 language codes | edit text inside `.bundle` archives (unpack first) |
| keep many documents open at once | undo, automatic backup, version history |

There is no undo. Treat every save as final and prefer a new output file.

## Connect a client

The package is a .NET tool, launched through `dnx` (ships with the .NET 10 SDK), so nothing is
installed globally:

```
dnx Witcher3StringEditor.MCP@0.1.0-beta.4 --yes
```

`--yes` skips the first-run download prompt; the `@version` pin is what makes an upgrade explicit.

VS Code (`.vscode/mcp.json`) and Visual Studio (`.mcp.json` next to the solution):

```json
{
  "servers": {
    "w3strings": {
      "type": "stdio",
      "command": "dnx",
      "args": ["Witcher3StringEditor.MCP@0.1.0-beta.4", "--yes"]
    }
  }
}
```

Cursor (`.cursor/mcp.json`) and Claude Desktop (`claude_desktop_config.json`) use the same block
under the key `mcpServers` instead of `servers`; Cursor additionally does not need `type`.
Claude Code:

```powershell
claude mcp add w3strings -- dnx Witcher3StringEditor.MCP@0.1.0-beta.4 --yes
```

Restart the client after editing a config file. A healthy connection lists **15 tools**, from
`open_document` to `list_languages`.

If the pinned version reports `NU1101` (package or version not found), the pin is stale or the
version was never pushed: look up the current one at
`https://registry.modelcontextprotocol.io/v0/servers?search=witcher3stringeditor` (the entry with
`isLatest: true`) or `https://api.nuget.org/v3-flatcontainer/witcher3stringeditor.mcp/index.json`,
then use that number.

## The 15 tools

Every tool except `open_document`, `create_document` and `list_languages` takes the `handle`
returned by the first two.

| Tool | Arguments | Notes |
| --- | --- | --- |
| `open_document` | `filePath` | Returns a handle. Read-only. **Same path returns the same handle**: a document already open is never re-read from disk |
| `create_document` | - | Empty document, already dirty, has no path |
| `count` | `handle` | Number of entries |
| `is_dirty` | `handle` | true after any real change or for a new document; false after a save |
| `navigate` | `handle`, `offset`, `strId?` | `offset` 0 returns the reference item, positive moves forward, negative backward; omit `strId` to start at the first item; returns null when out of range |
| `get_item` | `handle`, `strId` | One item |
| `search_items` | `handle`, `query`, `caseSensitive` | Substring over `strId`, `text` and `keyName`; returns **every** match |
| `add_item` | `handle`, `item` | `item` is `{strId, keyName?, keyHex?, text}`; duplicate `strId` is rejected |
| `update_item` | `handle`, `strId`, `text?`, `keyName?`, `keyHex?` | Omitted argument keeps its value, empty string clears it; at least one is required |
| `update_item_id` | `handle`, `strId`, `newStrId` | Must not collide |
| `update_item_key_name` | `handle`, `strId`, `keyName` | Also clears the key hash, which is recomputed on save |
| `delete_item` | `handle`, `strId` | Removes one entry |
| `save_document` | `handle`, `version`, `filePath?`, `language` | `version` is always required; `filePath` defaults to the current path; `language` defaults to `en` |
| `close_document` | `handle`, `saveBeforeClose` | Default **discards** unsaved changes; saving before close needs the document to have a path |
| `list_languages` | - | The 18 language codes |

Shapes: an item is `{strId, keyHex, keyName, text}` (ids and hashes are strings), and a search
result is `{handle, query, isCaseSensitive, matchCount, items}`.

## Five rules that prevent damage

1. **Save to a new file.** `save_document` without `filePath` overwrites the document's current
   path, and the game install is not a place to experiment. Confirm the target path with the user.
2. **Pass the original version and language.** The document does not remember either one: `version`
   is mandatory, and `language` silently falls back to `en`. A wrong language does not merely label
   the file, it changes the obfuscation key of every id and text, so the game reads garbage.
3. **Never let two handles own one path.** Saving onto a path that another open document owns is
   rejected; close or re-target the other document first.
4. **Re-open to verify a real read.** Because `open_document` returns the live in-memory document
   for a path, verifying an in-place save needs `close_document` first. Saving to a *new* path and
   opening that path reads the disk and is the cleaner check.
5. **Handles die with the server process.** After a client restart every handle is invalid; open
   the file again.

## Recovering the two facts the tools do not return

`open_document` returns only a handle, so inspect the file yourself before saving.

**Container version** - the first 8 bytes are `RTSW` followed by a little-endian `uint32`:

```powershell
$b = [IO.File]::ReadAllBytes($path)[0..7]
[Text.Encoding]::ASCII.GetString($b, 0, 4)   # RTSW
[BitConverter]::ToUInt32($b, 4)              # 162 / 163 / 164
```

`162` and `163` are the UTF-16LE generation and are both written as `162`; `164` is the UTF-8
generation. Keep the generation the file already has.

**Language** - not recoverable from the container: a language key of 0 (shared by `ar`, `br`, `cn`,
`esMX`, `kr`, `tr`, `ua`) means "no language", and the tool's `keyName` is always empty on load
because the format stores hashes, not names. Infer the language from the file name (`cn.w3strings`,
`en.w3strings`) and ask the user when the name is ambiguous. `list_languages` returns:
`ar br cn cz de en es esMX fr hu it jp kr pl ru zh tr ua`, where `cn` is Simplified Chinese,
`zh` Traditional, `br` Portuguese (Brazil), `cz` Czech, `jp` Japanese, `kr` Korean, `ua` Ukrainian.

## Routing

| The user wants | Load |
| --- | --- |
| find, change, add or delete entries; save and verify; bulk replacements | `w3strings-edit` |
| translate or localize text, keep a glossary, QA a translation | `w3strings-translate` |
| ship the result as a mod, pick the right version/language slot, test in game | `w3strings-mod` |

Those skills are siblings of this one. If they are not installed, the tool table plus rules above
are enough for simple single-item edits.

## What to ask before the first write

- the **full path** of the source file and whether the user owns a copy (game installs are
  read-only for normal users, and should stay untouched);
- the **output path** - default to a new file name, never the original;
- the **version** (`162`/`163` classic, `164` Remastered) - follow the source header;
- the **language code** - follow the file name;
- for many edits, confirmation that the user accepts batch work without a preview of every row.

## Errors: the client shows nothing useful

Clients print only `An error occurred invoking '<tool>'.`, because the server lets exceptions
surface without rewriting them. The real message is in the log:

- Windows `%LOCALAPPDATA%\Witcher3StringEditor.MCP\logs\`
- Linux and macOS `~/.local/share/Witcher3StringEditor.MCP/logs/`

The file rotates daily (`logYYYYMMDD.txt`), at 10 MB, keeping three files. Logs never go to stdout
or to the working directory.

| Symptom | Likely cause | Fix |
| --- | --- | --- |
| No tools at all after restart | `dnx` not resolvable, or the pinned version does not exist | `dnx --help` must work (else install the .NET 10 SDK); check the version |
| Every call fails generically | handle from a previous server process, or an argument rejected by validation | re-open the file; check the log for the real message |
| `open_document` fails | wrong path, not a `.w3strings` container, unreadable file | verify the path and the `RTSW` header |
| `get_item` / `update_item` fails | the entry does not exist, or the id carries whitespace, a sign or `0x` | ids are plain 32-bit decimals (`01` is accepted and normalized, `" 1"` and `-1` are not) |
| `save_document` fails | unsupported version, missing path, or path owned by another handle | pass `162`/`163`/`164`; pass a free output path |
| `close_document(saveBeforeClose=true)` fails | the document has never been saved, so there is no path | `save_document` with a path first |
| Game shows garbage or missing text | version/language did not match the file's generation or slot | re-save with the original `version` and `language` |

## Scale expectations

A retail `cn.w3strings` holds roughly 98,000 entries (about 7 MB); a mod file a few thousand.
Opening and saving such a file takes well under a second, but `search_items` serializes **all**
matches, so a common word can return tens of thousands of items and swamp the conversation.
Report `matchCount` with a handful of samples instead of pasting everything.
