# Witcher 3 String Editor MCP

<!-- mcp-name: io.github.0x7A2C9E5D/Witcher3StringEditor.MCP -->

A local (stdio) [MCP](https://modelcontextprotocol.io/) server for reading and editing
The Witcher 3 `.w3strings` localization files — inspect entries, search text, translate or replace
strings, add/delete entries, and save as version `162`/`163`/`164`.

Every operation works on an **in-memory document** identified by a `Guid` handle; only
`save_document` writes to disk, so you can edit a game file without touching the original.

## Tools

| Tool | Description |
| --- | --- |
| `open_document` | Open a `.w3strings` file, returns a document handle |
| `create_document` | Create a new empty document |
| `count` | Number of entries in a document |
| `is_dirty` | Whether the document has unsaved changes |
| `navigate` | Move relative to an entry (`offset`, optional `strId`) |
| `get_item` | Get one entry by `strId` |
| `search_items` | Substring search over id, text and key name |
| `add_item` | Add a new entry |
| `update_item` | Change text / key name / key hash of an entry |
| `update_item_id` | Change the `strId` of an entry |
| `update_item_key_name` | Set the key name (in memory; only the hash is saved) |
| `delete_item` | Delete an entry |
| `save_document` | Write the document to disk (`version`, `filePath`, `language`) |
| `close_document` | Close a document and release its handle |
| `list_languages` | List the supported language codes |

## Install

The package is a .NET tool, so clients launch it through
[`dnx`](https://learn.microsoft.com/dotnet/core/tools/dotnet-tool-execute), which ships with the
.NET 10 SDK. There is nothing to install globally and nothing to add to `PATH` — the first launch
downloads the package from NuGet.org.

All configurations below launch the same stdio command:

```
dnx Witcher3StringEditor.MCP@0.1.0-beta.4 --yes
```

`--yes` skips the download confirmation prompt; `@0.1.0-beta.4` pins the version, so upgrade by
editing that number and restarting the client.

### VS Code

Create `.vscode/mcp.json` in the workspace (or run **MCP: Add Server** from the Command Palette):

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

### Visual Studio

Create `.mcp.json` next to the `.sln`/`.slnx`. The `servers` block is identical to the VS Code one
above; Visual Studio reads the same file format.

### Cursor

Create `.cursor/mcp.json` in the project (or `~/.cursor/mcp.json` for all projects). Cursor uses the
`mcpServers` key:

```json
{
  "mcpServers": {
    "w3strings": {
      "command": "dnx",
      "args": ["Witcher3StringEditor.MCP@0.1.0-beta.4", "--yes"]
    }
  }
}
```

### Claude Code

```powershell
claude mcp add w3strings -- dnx Witcher3StringEditor.MCP@0.1.0-beta.4 --yes
```

### Claude Desktop

Edit `%APPDATA%\Claude\claude_desktop_config.json` (macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "w3strings": {
      "command": "dnx",
      "args": ["Witcher3StringEditor.MCP@0.1.0-beta.4", "--yes"]
    }
  }
}
```

Restart Claude Desktop after editing the file.

### Running from source

Before a release, or while developing, point the client at the project instead of the published
package:

```json
{
  "servers": {
    "w3strings-dev": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "Witcher3StringEditor.MCP/Witcher3StringEditor.MCP.csproj"]
    }
  }
}
```

### Without the .NET 10 SDK

Publish the self-contained build and point `command` at the executable instead of `dnx`:

```powershell
dotnet publish .\Witcher3StringEditor.MCP -c Release -r win-x64
```

```json
{
  "servers": {
    "w3strings": {
      "type": "stdio",
      "command": "C:\\path\\to\\Witcher3StringEditor.MCP\\bin\\Release\\net10.0\\win-x64\\publish\\Witcher3StringEditor.MCP.exe"
    }
  }
}
```

### Verify the connection

After restarting the client, the server should expose **15 tools** (`open_document` …
`list_languages`). If the client lists none, or every call fails with a generic message:

- Check that `dnx` resolves: `dnx --help` must work in a terminal (otherwise install the .NET 10 SDK).
- Check that the configured version actually exists on NuGet.org.
- Read the log — the client only shows a generic error, the real exception is always in
  `%LOCALAPPDATA%\Witcher3StringEditor.MCP\logs\log.txt`.

## Build from source

```powershell
dotnet run --project .\Witcher3StringEditor.MCP           # run from source
dotnet pack -c Release -r win-x64                        # build the win-x64 tool package
```

`dotnet pack` produces one platform-independent package plus one package per runtime identifier;
all of them must be pushed to NuGet.org for `dnx` to resolve the right binary.

## Notes

- `.w3strings` stores no readable key name, only an 8-digit uppercase hex hash; key names are empty
  after reopening a file.
- `163` is written as `162`. Save with `164` for the Remastered edition, `162`/`163` for the classic
  and next-gen builds.
- Zero-hash key entries are dropped on save.
- The server has no undo: save to a new file when editing game content.
- Diagnostics go to the user's local application data, never to stdout and never to the working
  directory: `%LOCALAPPDATA%\Witcher3StringEditor.MCP\logs\log.txt` on Windows and
  `~/.local/share/Witcher3StringEditor.MCP/logs/log.txt` on Unix-like systems. The file rotates at
  10 MB and the last three files are kept.

## License

See the [repository](https://github.com/0x7A2C9E5D/Witcher3StringEditor.MCP).
