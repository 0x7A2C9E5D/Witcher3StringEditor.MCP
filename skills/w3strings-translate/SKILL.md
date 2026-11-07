---
name: w3strings-translate
description: Translate The Witcher 3 .w3strings text through the W3Strings MCP server while preserving markup, placeholders, and glossary terms, then save it as a language file of the right version. Use for 汉化/翻译/本地化 of game strings, building a Chinese or other language file, or QA of an existing translation.
---

# Translating .w3strings text

Translation is an editing workflow with two extra risks: **markup that must survive byte for byte**
and **terminology that must stay consistent across tens of thousands of entries**. The tools are the
ones from `w3strings-edit`; this skill is about doing the job well, not about new calls.

There is no CSV or Excel export. Texts are read with `get_item` / `search_items` and written back
with `update_item`; the translation happens in the conversation, so volumes must be chunked and
agreed with the user up front.

## 1. Decide which slot the output belongs to

Two different jobs look similar:

| Job | Source file | `save_document` language | Output |
| --- | --- | --- | --- |
| improve / rewrite text that already exists in that language | `cn.w3strings` | `cn` | edited copy of the same slot |
| create a translation of another language's text | `en.w3strings` | the **target** code, e.g. `cn` | new slot file, named `cn.w3strings` |

Saving re-obfuscates every id and text with the language you pass, so a translated `en` document
saved as `cn` is a valid `cn` container. Never pass the source language by reflex: read the file
name, decide the target, and state both to the user before writing.

Keep the container version identical to the source (`162`/`163` UTF-16LE, `164` UTF-8). A version
change between source and output is what makes a working translation render as garbage.

## 2. Inventory the non-prose

Before translating anything, sample 20-30 entries across the file and list every token that is not
prose. `.w3strings` text routinely carries:

- line breaks and escaped sequences (`\n`, literal newlines);
- markup `<b>…</b>`, `<i>…</i>`, colour/format tags;
- placeholders (`%s`, `%d`, `{0}`, `#`, `|`, brackets, quotes used as markup);
- leading/trailing spaces that separate UI fragments.

Rules that follow from them: the token multiset of the translation must equal the source's, tags are
never translated, and an entry whose tokens you cannot identify is left untouched and reported, not
guessed.

## 3. Build the glossary first

Write down the mapping before the first `update_item` - names, places, titles, item categories,
UI verbs. Prefer the terms the user gives, then the official localized game terms, and keep a single
choice per term.

Tell the user the glossary once, in a compact table, and reuse it verbatim. Inconsistency across
98,000 entries is the defect a reviewer notices first.

## 4. Translate in visible chunks

1. pick a slice the user can review (an id range, a `search_items` result, a category of strings);
2. show the first 3-5 entries as `old → new` and get approval on tone, formality and length;
3. apply that style to the rest of the slice with one `update_item` per entry;
4. report progress as counts, not as a wall of text;
5. after each slice, note any entry you skipped and why.

Keep a rule of thumb for length: menu, button and subtitle strings must stay short enough not to
overflow their UI box; dialogue can be longer. Chinese is usually shorter than English, so leftover
padding spaces are a common artefact - remove spaces that only existed to align Latin text, but keep
them inside concatenation fragments.

## 5. QA before saving

- `count` before and after: identical.
- No entry's text became empty.
- Placeholder and tag multiset equal per changed entry (check the entries you changed, not the file).
- Sample re-reads via `get_item` for the riskiest entries: long ones, ones with tags, ones with
  quotes.
- No machine leftovers: a `search_items` on a frequent source word (for example `the`, `and`) should
  return only entries you deliberately kept in the source language; if it returns thousands, a slice
  was skipped or saved against the wrong handle.

Then `save_document` to the agreed path with the target `language` and the source `version`, and
verify by opening the output and reading a few ids.

## 6. Report to the user

State: source path, output path, version, target language, number of entries read/changed/skipped,
the glossary decisions, and anything left untranslated. Do not claim a complete translation if a
slice was sampled rather than processed - say how much of the file the pass covered.

For shipping the result as a mod (folder layout, load order, in-game check), continue with
`w3strings-mod`.
