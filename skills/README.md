# Skills for the W3Strings MCP server

Agent skills that teach an AI assistant to use this server correctly: which tool to call, what the
container format does not tell you, and where the mistakes are. Each skill is a folder with a
`SKILL.md` (YAML frontmatter plus Markdown), the format used by the DSH, Claude Code and other
Agent Skills clients - no build step, no dependencies.

| Skill | Load it when |
| --- | --- |
| [`w3strings-mcp`](w3strings-mcp/SKILL.md) | installing/connecting the server, choosing a tool, general "what can this do", error triage |
| [`w3strings-edit`](w3strings-edit/SKILL.md) | finding, changing, adding, deleting entries; saving and verifying; bulk replacements |
| [`w3strings-translate`](w3strings-translate/SKILL.md) | translating or localizing text, glossaries, markup, translation QA |
| [`w3strings-mod`](w3strings-mod/SKILL.md) | shipping the result as a mod: paths, language slot, version, load order, testing |

`w3strings-mcp` is the entry point; the other three are scenario skills that it routes to. They are
independent files - install the whole folder, but loading one does not require the others (the tool
table lives in `w3strings-mcp`).

## Install

Copy the folders into the skills directory of the client you use. DSH reads
`%USERPROFILE%\.dsh\skills`, Claude Code reads `%USERPROFILE%\.claude\skills` (macOS/Linux
`~/.dsh/skills`, `~/.claude/skills`):

```powershell
$target = "$env:USERPROFILE\.dsh\skills"
New-Item -ItemType Directory -Force -Path $target | Out-Null
Get-ChildItem .\skills -Directory | ForEach-Object {
    $dest = Join-Path $target $_.Name
    Remove-Item $dest -Recurse -Force -ErrorAction SilentlyContinue   # reinstall cleanly
    Copy-Item $_.FullName $dest -Recurse
}
```

Alternatively point the client's skill directory at this folder's parent if it supports an extra
search path. After copying, the skill list should show `w3strings-mcp`, `w3strings-edit`,
`w3strings-translate` and `w3strings-mod`.

The skills assume the **server** is installed and connected, which is a separate step (via `dnx`,
see the [main README](../Witcher3StringEditor.MCP/README.md#install)). A skill is documentation; it
cannot start the server for you.

## Version pins

Every skill quotes the published tool version in the `dnx` command
(`Witcher3StringEditor.MCP@0.1.0-beta.4`). The pin must match the released package, which lives in
two places:

- `Witcher3StringEditor.MCP/Witcher3StringEditor.MCP.csproj` → `PackageVersion`
- `Witcher3StringEditor.MCP/.mcp/server.json` → `version`

When a release bumps them, bump the pins too:

```powershell
# what the skills claim
Select-String -Path .\skills\*\SKILL.md -Pattern 'Witcher3StringEditor\.MCP@[0-9][^`"\s]*' |
    ForEach-Object { "{0}:{1}  {2}" -f (Resolve-Path -Relative $_.Path), $_.LineNumber, $_.Matches[0].Value }

# what is actually shipped
dotnet msbuild -getProperty:PackageVersion .\Witcher3StringEditor.MCP\Witcher3StringEditor.MCP.csproj
```

A stale pin is not dangerous (a client fails with `NU1101`, and `w3strings-mcp` tells the agent to
look up the latest version in the registry), but it is the first thing a new user hits, so keep them
equal. The registry entry to check is
`https://registry.modelcontextprotocol.io/v0/servers?search=witcher3stringeditor` - the one with
`isLatest: true`.

## Adding a skill

Create `skills/<name>/SKILL.md` with frontmatter:

```markdown
---
name: <name>                          # must equal the folder name
description: <one line, with the words a user would say>   # drives routing, no ": " inside
---
```

Then add a row to the table above. Keep descriptions trigger-oriented (what the user asks for, not
what the file contains) and keep skills non-overlapping: if a new skill has to repeat more than a
paragraph from an existing one, it belongs in that existing skill instead.
