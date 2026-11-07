---
name: w3strings-mod
description: Package edited or translated .w3strings text as a working Witcher 3 mod - correct mod folder path, language slot, container version for the game build, load order, in-game test and rollback. Use for 巫师 3 文本 mod 打包、语言包/汉化包制作, strings that do not appear in game, or diagnosing a mod that loads but shows no text.
---

# Shipping .w3strings as a mod

Editing is only half the job: a file that is correct but sits at the wrong path, under the wrong
name, or in the wrong container generation changes nothing in the game. Work through this list in
order and say out loud which step a failure belongs to.

## 1. The game's own files are read-only material

The retail strings live in the install, next to the archives:

```
<game>\content\content0\<language>.w3strings      e.g. cn.w3strings
```

Copy the file out and edit the copy. Do not save over the install: it is easy to corrupt (no undo,
no backup from the server), launchers may overwrite it, and the user then has no clean reference.

## 2. A mod mirrors `content\content0` under its own folder

A mod's `content` folder maps to the game's `content0` folder, so the override for a language file
is:

```
<game>\mods\<mod name>\content\<language>.w3strings
```

- the **file name** must be the language the game loads for the user's language setting (`cn`,
  `zh`, `en`, ...), not the language you translated from;
- the **container inside** must be saved with the matching `language` - a `cn` file whose ids and
  texts were obfuscated as `en` is unreadable text as far as the game is concerned;
- one file per language. A translation mod ships exactly one slot file.

Confirm the layout with one visible string before distributing: a single changed line is enough to
prove path, name and obfuscation at once. If nothing changes, the file is in the wrong place or the
wrong slot - not a translation problem.

## 3. Match the container version to the game build

| Generation | Version | Encoding | Notes |
| --- | --- | --- | --- |
| classic / older next-gen | `162` or `163` | UTF-16LE | both are written as `162` |
| current Remastered / next-gen | `164` | UTF-8 | one byte per character |

Read the version the game itself uses instead of trusting a table:

```powershell
$b = [IO.File]::ReadAllBytes("<game>\content\content0\cn.w3strings")[0..7]
[BitConverter]::ToUInt32($b, 4)   # 162 / 163 / 164
```

Save with that number. A `164` file in a `162` build (or the reverse) is either ignored or rendered
as mojibake, and mixing generations is the most common reason a carefully translated file does
nothing.

## 4. Load order and conflicts

- Two mods that ship the same `<language>.w3strings` cannot both take effect; the higher priority
  wins. Say so before the user installs a second text mod.
- Enable and order mods in `Documents\The Witcher 3\mods.settings` (Windows), one block per mod:

  ```ini
  [modMyTranslation]
  Enabled=1
  Priority=10
  ```

  Newer game versions also expose this through the mods menu.
- Full merges are manual: there is no merge tool here. Open the winning document and apply the
  other mod's changed entries by id, or ask the user which mod should win. Do not concatenate two
  containers bytewise.

## 5. Packaging

Ship the mod tree, zipped, with the folder name matching the `mods.settings` section:

```
modMyTranslation\
  content\
    cn.w3strings
```

If the mod also carries scripts or bundle edits, they go in the same mirrored tree; for text-only
work ship the single `.w3strings`. Confirm after zipping that the archive root is `modMyTranslation`
and not the files themselves, and that the file name has not been renamed (no `cn (1).w3strings`,
no BOM added, no re-encoding by an editor).

## 6. Test, then hand over

1. install into `<game>\mods\`, enable in `mods.settings`, start the game;
2. check one string that lives in a menu and one in dialogue - menus are cached differently from
   dialogue and expose different failures;
3. if the game shows blank or garbled text, re-check version and language before touching the text;
4. if the game ignores the mod entirely, check the path, the file name and the priority.

Tell the user the exact rollback: delete the mod folder (and its `mods.settings` block) to return to
the retail strings, and keep the pre-edit copy they should have made.

## Deliverables to state

```
source     <original path>
output     <mods\modName\content\cn.w3strings>
version    162 | 163 (written 162) | 164
language   cn
entries    <count> (<changed> changed, <skipped> skipped)
tested     <one menu string, one dialogue string> | not tested
rollback   delete <game>\mods\<modName>
```

If any of these are unknown - especially "tested" - say so instead of implying the mod works.
