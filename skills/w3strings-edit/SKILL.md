---
name: w3strings-edit
description: Locate, change, add, or delete entries in a .w3strings document through the W3Strings MCP server, then save it as the right container version and language and verify the result. Use for Witcher 3 text edits, fixing one line, batch or bulk replacements, changing entry ids and key hashes, or 巫师 3 文本修改/批量替换/条目增删.
---

# Editing .w3strings entries

This is the procedure for touching entries: locate, change, save, verify. It assumes the
`w3strings-mcp` skill (the tool table and the five safety rules) is loaded or that you have read
it; the points repeated here are the ones that bite during editing.

## Before the first call

Collect four answers, asking the user rather than guessing:

1. **source path** - is it a copy the user may write to, or the game install?
2. **output path** - default to a new file; never overwrite the original.
3. **version** - read it from the header, do not infer it from what the file is called:

   ```powershell
   $b = [IO.File]::ReadAllBytes($path)[0..7]
   [BitConverter]::ToUInt32($b, 4)   # 162 / 163 / 164
   ```

   `162`/`163` are UTF-16LE and are written as `162`; `164` is UTF-8. Keep the generation.
4. **language code** - from the file name (`cn.w3strings` → `cn`). The container does not expose
   it, and `save_document` defaults to `en`, which silently re-obfuscates every id and text for
   the wrong language. If the file name is not a language code, ask.

## Inspect

```
open_document(filePath) → handle
count(handle)                       # entries, not bytes
navigate(handle, offset: 0)         # first item, or an anchor when strId is given
get_item(handle, strId)             # one item
```

Use `navigate` with a small positive/negative `offset` to read a neighbourhood instead of dumping
the file. Items come back as `{strId, keyHex, keyName, text}`.

## Find

`search_items(handle, query, caseSensitive)` is a plain substring match over `strId`, `text` and
`keyName`. Consequences worth telling the user:

- **no regex, no word boundaries, no field scoping** - `"Geralt"` also hits `"Geralt's"`;
- it returns **all** matches with full text, so a frequent word can produce tens of thousands of
  items. Start with `matchCount` plus a few samples, then ask whether to proceed;
- `caseSensitive` defaults to `false`;
- `keyName` is empty for every entry read from a file (the format stores an 8-digit hash, not the
  name), so searching by readable key only works on entries whose name you set in this session.
  To find entries by key, search the text or the `strId` instead.

Empty queries are rejected.

## Change

| Goal | Call | Traps |
| --- | --- | --- |
| replace text | `update_item(handle, strId, text: "...")` | passing the same text is a no-op and does not dirty the document; empty string clears |
| set a key name | `update_item_key_name(handle, strId, keyName)` | clears `keyHex`; at save the name is hashed and becomes the stored key, so this changes gameplay lookups - it is not a comment field |
| set a raw key hash | `update_item(handle, strId, keyHex: "1A2B3C4D")` | 32-bit hex, up to 8 digits; `""` clears it and the entry is saved without a key (a zero hash is never written) |
| add an entry | `add_item(handle, item: {strId, text, keyName?, keyHex?})` | `strId` must be an unused 32-bit decimal; new entries land at the end of the document |
| delete | `delete_item(handle, strId)` | no confirm, no undo |
| renumber | `update_item_id(handle, strId, newStrId)` | must not collide with another entry |
| confirm state | `is_dirty(handle)` | true only if something actually changed |

Ids are canonicalized (`"01"` becomes `"1"`; whitespace, signs and `0x` prefixes are rejected), so
`search_items` output can be passed straight back in.

`update_item` requires at least one of `text`, `keyName`, `keyHex`; omitting an argument keeps its
value while an empty string clears it. That difference is the most common source of accidental
data loss - use `get_item` first when in doubt.

## Save and verify

Save to a **new** path, then read that path back:

```
save_document(handle, version: "164", filePath: "D:\work\cn-mod.w3strings", language: "cn")
open_document("D:\work\cn-mod.w3strings") → verifyHandle      # a fresh disk read
get_item(verifyHandle, "<one changed id>")
count(verifyHandle)                                            # same as the edited document
close_document(verifyHandle)
close_document(handle)                                         # discards nothing; already saved
```

Overwriting the current path is different: `open_document` on an already-open path returns the
live in-memory document, so a reopen proves nothing. For an in-place save, `close_document` the
handle first, or verify against a copy on disk. The server takes no backup, so copy the target
before overwriting it (`Copy-Item target target.bak`).

Saving clears the dirty flag and adopts the new path; the handle stays usable afterwards.

## Batch edits

There is no bulk tool and no CSV import/export. For more than a handful of entries:

1. resolve the full match set and tell the user `matchCount` and a few samples;
2. agree on the rule (for example "only entries whose text starts with `Quest:`");
3. work in chunks of roughly 20-50 entries, one `update_item` per entry, then report progress in
   one short line (`done 50/213`) rather than echoing every row;
4. on a failed update, stop the chunk, report the id and the log message, and ask before continuing;
5. at the end, save once and verify by re-opening the output.

If the user abandons a batch, `close_document` without saving leaves the source file untouched: all
edits were in memory.

## Failure modes seen in practice

| Message in the log | Cause |
| --- | --- |
| `A string item with id N does not exist.` | the entry was not loaded (wrong handle, wrong file) or was deleted earlier in the session |
| `A string item with id N already exists.` | `add_item` or `update_item_id` collided |
| `String id 'x' is not a 32-bit unsigned integer.` | whitespace, sign, hex or a value above 4294967295 |
| `The path is already open as document <guid>` | another open document owns the save target; close it or pick another path |
| `filePath is required to save a document that has no path yet.` | a `create_document` document must be given a path on its first save |
| silent `save_document` returning false / an error without detail | check the log; the client shows only a generic message |

## Do not

- do not edit the game's own `content0` strings in place;
- do not assume `search_items` returns a partial page - it returns everything;
- do not claim text was saved without a `save_document` plus a read-back;
- do not hand-edit `keyHex` unless the user supplied the hash: a key name is hashed at save time and
  overwrites whatever `keyHex` an entry carries.
