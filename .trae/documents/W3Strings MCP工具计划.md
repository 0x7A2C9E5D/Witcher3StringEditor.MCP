# W3Strings MCP 工具计划

> 本文件是 W3Strings MCP 工具的唯一设计与实施文档，已整合历史文档，并更新到最新：
> 单个 `navigate` 工具以偏移量在文档中上下移动并返回到达的条目，`search_items` 返回全部命中；
> `open_document` / `create_document` 直接返回 `Guid` Handle，条目数与脏标记由 `count` / `is_dirty` 查询；
> 不再分页，也不再提供 `list_items` / `previous_item` / `next_item` / `get_info`。

## 一、目标与范围

在 `Witcher3StringEditor.MCP` 中提供"文档状态由 MCP 管理"的 W3Strings 工具集：

- `open_document` / `create_document` 打开或新建文档，返回 **Handle**。
- 其余工具全部通过 Handle 操作进程内存中的文档状态。
- 以 32 位无符号 `StrId` 定位条目，支持条目级增删改查、搜索、重命名。
- 改动只留在内存，`save_document` 显式写盘，`close_document` 释放 Handle。
- 序列化器项目 `Witcher3StringEditor.Serializers` 仅调整 `Deserialize` / `Serialize` 的签名，MCP 层调用其公开 API。
- **`navigate` 一次返回一条**：接收 `offset` 与可选的 `strId`。省略 `strId` 时以第一条为基准；
  `offset = 0` 返回基准条目，正数向后移动（下一条），负数向前移动（上一条）；目标越界返回 `null`。
- **`search_items` 一次返回全部命中**：接收 `query` 与 `caseSensitive`，返回 `matchCount` 与
  `items` 数组；不接受游标。
- **Handle 由创建类工具直接返回**：`open_document` / `create_document` 的返回类型就是 `Guid`，
  不再包装任何摘要字段。
- **文档状态由两个只读工具查询**：`count` 返回条目数，`is_dirty` 返回是否有未保存改动
  （新建文档或任意条目改动后为 `true`，`save_document` 后为 `false`）。
- **文件名必须与语言代码一致**：游戏只加载 `<language>.w3strings`（如 `en.w3strings`），
  该规则写入 `save_document` / `open_document` / `list_languages` 的工具说明，作为给 Agent 的提示。

本次变更范围：移除 `list_items`、`previous_item`、`next_item`，合并为单个 `navigate` 工具；
移除 `get_info` 与 `W3StringsDocumentInfo`，`open_document` / `create_document` 改为直接返回 `Guid`，
`save_document` 返回 `bool` 报告写盘是否成功，新增 `count` / `is_dirty` 两个只读工具；
在工具说明中加入文件名与语言代码一致的提示；工具总数为 15 个。

## 二、现状与架构

### 2.1 序列化器公开能力

- `W3StringsSerializer.Deserialize(filePath)` → `Task<W3StringsDocument>`，返回条目（`Items`）、
  语言（`Language`，语言 key 为 0 时为 `null`）与编码（`Encoding`），失败抛 `W3StringsException`。
- `W3StringsSerializer.Serialize(document)` → `Task<(bool, Stream?)>`，使用文档的 `Items`、`Language`、
  `Encoding`；`Language` 为 `null` 或写入失败时返回 `(false, null)`。
- 容器版本绑定编码：162 / 163 → `Encoding.Unicode`（UTF-16LE，经典版与次世代版），164 → `Encoding.UTF8`
  （重置版）；版本由游戏版本决定，不能混用。写盘时 162 / 163 统一写为 162，164 写为 164。
  版本只在保存时决定：`save_document` 必须显式指定版本；内存文档不持有路径、语言或编码。
  `close_document(saveBeforeClose=true)` 在关闭前保存时，从现有文件头读取语言与版本。
- `StringItem` 公开 record：`StrId`、`KeyHex`、`KeyName`、`Text`，属性可读写。
- `W3Language` 提供 `All`、`Default`、`FromCode(code/cultureCode)`、`Key`、`Magic`。
- `W3StringsBuilder` 写盘时以 `KeyName` 优先计算 hash 并回写 `StringItem.KeyHex`；`KeyHex` 为空表示"无 key"；
  重复或非法 `StrId` 会导致整体序列化失败。

### 2.2 MCP 项目现状

- [Program.cs](file:///c:/Users/ROG/source/repos/Witcher3StringEditor.MCP/Witcher3StringEditor.MCP/Program.cs)：
  基于 `Host.CreateApplicationBuilder` 的控制台主机，`AddMcpServer().WithStdioServerTransport()` 启用
  stdio 传输，集中配置日志；组合 `McpJsonUtilities.DefaultOptions` 与源生成
  `W3StringsJsonContext`，注册 `W3StringsDocumentTools`。
- [Witcher3StringEditor.MCP.csproj](file:///c:/Users/ROG/source/repos/Witcher3StringEditor.MCP/Witcher3StringEditor.MCP/Witcher3StringEditor.MCP.csproj)：
  `Microsoft.NET.Sdk`，引用 `ModelContextProtocol`，`PublishAot`，已引用
  `Witcher3StringEditor.Serializers.csproj`。
- `Models/`（命名空间 `Witcher3StringEditor.MCP.Models`）：每个模型一个文件，包括
  `W3StringsSearchResult`、`W3StringsDocument`，以及
  [W3StringsJsonContext.cs](file:///c:/Users/ROG/source/repos/Witcher3StringEditor.MCP/Witcher3StringEditor.MCP/Models/W3StringsJsonContext.cs)
  （`[JsonSerializable]` 源生成上下文，camelCase）。新增返回类型必须登记，否则 AOT 下无法序列化。
- `Storage/`（命名空间 `Witcher3StringEditor.MCP.Storage`）由 `internal static partial class DocumentStorage`
  组成，按职责拆分为 3 个文件：
  - [DocumentStorage.cs](file:///c:/Users/ROG/source/repos/Witcher3StringEditor.MCP/Witcher3StringEditor.MCP/Storage/DocumentStorage.cs)：
    共享状态与辅助。Handle 注册表、双向路径映射、`SemaphoreSlim`，
    以及 `ResolveLanguage` / `ResolveVersion` / `Find` / `FindItem` / `Clone` / 规范化等私有静态方法。
  - [DocumentStorage.Lifecycle.cs](file:///c:/Users/ROG/source/repos/Witcher3StringEditor.MCP/Witcher3StringEditor.MCP/Storage/DocumentStorage.Lifecycle.cs)：
    文档生命周期。`extension(W3StringsDocument)` 静态扩展成员 `OpenAsync` / `CreateAsync` /
    `SaveAsync` / `CloseAsync` / `ExecuteAsync`，以及 `LoadAsync` / `ExecuteCoreAsync` / `SaveCoreAsync`。
  - [DocumentStorage.Operations.cs](file:///c:/Users/ROG/source/repos/Witcher3StringEditor.MCP/Witcher3StringEditor.MCP/Storage/DocumentStorage.Operations.cs)：
    文档自身操作。`extension(W3StringsDocument document)` 实例扩展成员 `WriteAsync` /
    `Navigate` / `GetItem` / `SearchItems`，以及条目增删改方法。
- [W3StringsDocumentTools.cs](file:///c:/Users/ROG/source/repos/Witcher3StringEditor.MCP/Witcher3StringEditor.MCP/Tools/W3StringsDocumentTools.cs)：
  `internal sealed class` + `public static` 方法 + `[McpServerTool(Name = "...")]`；无 Store 实例，
  工具直接调用 `W3StringsDocument.OpenAsync(...)` 等静态扩展成员，条目操作以 `W3StringsDocument.ExecuteAsync(handle, document => document.…)` 执行。
- 日志分两层：宿主日志在 `Program.cs` 用 `Microsoft.Extensions.Logging` 配置并只写 stderr（stdout 归协议），
  工具事件通过 `McpServer.AsClientLoggerProvider()` 以 `notifications/message` 发给客户端（见 4.7）。
- 无测试项目、无 `.w3strings` 样例文件；验证使用 `%TEMP%` 下的临时脚本与文件。

### 2.3 已实现能力（基线）

- 项目引用、JSON 源生成上下文、文档注册表与扩展成员、15 个工具、`Program.cs` 注册均已完成，已通过编译、
  stdio 端到端与 Native AOT 冒烟验证。
- `DocumentStorage`（`internal static partial class`，3 个文件）：
  - `DocumentStorage.cs`（共享状态与辅助）：注册表/双向路径映射/`SemaphoreSlim`、
    语言解析、版本→编码解析（`ResolveVersion`）、`Find`、`FindItem`、`Clone`、`StrId` / `KeyHex` 规范化；
  - `DocumentStorage.Lifecycle.cs`（文档生命周期，`extension(W3StringsDocument)` 静态扩展成员）：
    `OpenAsync`（返回 `Guid`）、`CreateAsync`（返回 `Guid`）、`SaveAsync`（返回 `bool`）、`CloseAsync`、
    `ExecuteAsync`、`LoadAsync`、`ExecuteCoreAsync`、`SaveCoreAsync`；
  - `DocumentStorage.Operations.cs`（文档自身操作，`extension(W3StringsDocument document)` 实例扩展成员）：
    `WriteAsync`、`Navigate`、`GetItem`、`SearchItems`、`AddItem`、
    `UpdateItem`、`UpdateItemId`、`UpdateItemKeyName`、`DeleteItem`。

## 三、工具清单（15 个）

| # | 工具 | 主要参数 | 状态变化 | 状态 |
| --- | --- | --- | --- | --- |
| 1 | `open_document` | `filePath` | 创建内存文档 | **返回 `Guid`** |
| 2 | `create_document` | 无 | 创建空内存文档 | **返回 `Guid`** |
| 3 | `count` | `handle` | 只读 | **新工具** |
| 4 | `is_dirty` | `handle` | 只读 | **新工具** |
| 5 | `navigate` | `handle`, `offset`, `strId?` | 只读 | **合并三个旧导航工具** |
| 6 | `get_item` | `handle`, `strId` | 只读 | 已实现 |
| 7 | `add_item` | `handle`, `item` | 修改内存 | 已实现 |
| 8 | `update_item` | `handle`, `strId`, `text?`, `keyName?`, `keyHex?` | 修改内存 | 已实现 |
| 9 | `delete_item` | `handle`, `strId` | 修改内存 | 已实现 |
| 10 | `save_document` | `handle`, `version`（必填）, `filePath?`, `language=en` | 写磁盘 | **返回 `bool`** |
| 11 | `close_document` | `handle`, `saveBeforeClose=false` | 删除内存文档 | 已实现 |
| 12 | `list_languages` | 无 | 只读 | 已实现 |
| 13 | `search_items` | `handle`, `query`, `caseSensitive=false` | 只读 | **返回全部命中** |
| 14 | `update_item_id` | `handle`, `strId`, `newStrId` | 修改内存 | 已实现 |
| 15 | `update_item_key_name` | `handle`, `strId`, `keyName` | 修改内存 | 已实现 |

注册方式为 `WithTools<W3StringsDocumentTools>(jsonOptions)`，本次变更无需修改 `Program.cs`。

## 四、数据与状态模型

### 4.1 内存文档

```csharp
internal sealed class W3StringsDocument
{
    public required Guid Handle { get; init; }          // 每个文档唯一，Guid.NewGuid() 生成
    public List<StringItem> Items { get; init; } = [];
    public bool IsDirty { get; set; }
}
```

路径、语言、编码只在保存时由参数提供，文档本身不持有。

### 4.2 存储与并发

- `SemaphoreSlim _gate` 串行化；`_documents`（Handle → 文档）、`_handlesByPath`（绝对路径 → Handle）、
  `_pathsByHandle`（Handle → 绝对路径，仅供保存时确定默认目标与关闭时清理）。
- Handle 为 `Guid`（`Guid.NewGuid()` 生成），JSON 侧是标准 GUID 字符串；工具参数按 `Guid` 绑定，
  无法解析的字符串在调用工具前即被拒绝。
- 同一路径只允许一个打开中的 Handle；重复 `open_document` 返回已有 Handle。
- 未保存的文档没有路径，`save_document` 首次保存时登记路径；目标已被其他 Handle 占用则报错。
  `save_document` 省略 `filePath` 时使用该 Handle 已登记的路径。
- `save_document` 的 `version` 必填，文档不保留版本；`close_document(saveBeforeClose=true)` 从文件头读取
  语言与版本后再写盘。
- 未知 Handle → `KeyNotFoundException`；未知 `StrId` → `KeyNotFoundException`。
- `navigate` 为无状态：服务端不记录浏览位置，客户端每次都传入参照条目的 `strId` 与偏移量。

### 4.3 模型与 JSON 上下文

文件：`Witcher3StringEditor.MCP/Models/`（命名空间 `Witcher3StringEditor.MCP.Models`，每个模型一个文件）

```csharp
internal sealed record W3StringsSearchResult(
    Guid Handle,
    string Query,
    bool IsCaseSensitive,
    int MatchCount,      // 命中总数
    StringItem[]? Items); // 全部命中条目
```

- `W3StringsJsonContext` 登记 `Guid`、`int`、`bool`、`string[]`、`W3StringsSearchResult`、`StringItem`
  与 `StringItem[]`；新增返回类型仍需登记。
- `open_document` / `create_document` 直接返回 `Guid`（JSON 中为 GUID 字符串），不再包装摘要字段；
  条目数由 `count`（`int`）、脏标记由 `is_dirty`（`bool`）查询。
- `save_document` 写盘成功后返回 `true`；写盘失败（序列化失败、路径不可写等）抛错，不会返回 `false`，
  因此返回 `false` 只可能出现在后续扩展中，调用方应按"有值即成功"判断。
- JSON 为 camelCase：`handle`、`query`、`isCaseSensitive`、`matchCount`、`items`。
- `Items` 是条目的快照副本（`Clone`）。

### 4.4 规范化规则

- `StrId`：解析为 32 位无符号十进制规范形式（`"01"` → `"1"`），非法值在入口报错。
- `KeyHex`：空/空白 → 空字符串；否则规范为 8 位大写十六进制，非法值报错。
- 条目返回统一使用副本（`Clone`），调用方拿到的是快照。

### 4.5 读取规则

`navigate`：

- 参数为 `handle`、`offset`（必填）与可选的 `strId`。
- 省略 `strId`（或传空白）时基准为文档第一条；否则以该 `strId` 对应条目的当前位置为基准。
- `offset = 0` 返回基准条目本身；正数向后移动（下一条），负数向前移动（上一条）。
- 目标位置落在文档范围外（含空文档、越过第一条/最后一条）时返回 `null`；
  工具层的 `null` 结果在 MCP 响应中表现为空的 `content` 数组。
- `strId` 非法时抛 `ArgumentException`，条目不存在时抛 `KeyNotFoundException`，由 SDK 转为工具错误。
- 返回的是副本（`Clone`）；排序保持文档原始顺序。
- 支持一次跨多条：`offset=4` 等价于连续 4 次下一条；删除条目后按当前顺序重新计算。

`search_items`：

- 参数为 `query` 与 `caseSensitive`；空查询报错（`"The search query must not be empty."`）。
- 按 `strId` / `text` / `keyName` 子串匹配（`caseSensitive ? Ordinal : OrdinalIgnoreCase`），
  返回全部命中（`items`，保持文档顺序，无命中时为空数组）与命中总数 `matchCount`。
- 不接受游标，没有分页或 `limit`。

### 4.6 文件名与语言的一致性（Agent 提示）

游戏只加载**文件名与语言代码一致**的 `.w3strings` 文件，因此工具描述中明确写入以下约定，
让 Agent 在调用 `save_document` 前就知道如何命名：

- 文件名（不含扩展名）必须就是语言代码：`<language>.w3strings`，例如 `en.w3strings`、
  `cn.w3strings`、`zh-Hans.w3strings`。
- 语言由 `save_document` 的 `language` 参数决定，文件名必须与之匹配；不能把已存在的文件改名后
  以另一语言保存（否则游戏加载不到）。
- `open_document` 打开的文件通常也按此命名；`list_languages` 返回的代码既可作为 `language` 参数，
  也可直接用作文件名。
- 该约定只写在工具说明（`[Description]`）里作为 Agent 提示，MCP 不对文件名校验或自动改名，
  由 Agent 按提示选择路径。

`save_document` 的说明片段：

> The game only loads a string file whose name matches its language code: always name the file
> `<language>.w3strings` (for example `en.w3strings` when language is `en`, `cn.w3strings` when
> language is `cn`) and never rename an existing file to a different language.

### 4.7 日志与传输

传输保持 stdio（`AddMcpServer().WithStdioServerTransport()`）：stdout 只承载 MCP 协议，不能混入日志。
日志按 MCP 2025-11-25 Logging 规范分两层实现。

**宿主日志：控制台 → stderr**

- `Program.cs` 中 `ClearProviders()` + `SetMinimumLevel(Information)` +
  `AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace)`，
  SDK 与 Microsoft.Hosting.Lifetime 的日志全部写 stderr，stdout 保持纯净。

**客户端日志：`notifications/message`**

- 规范要求：发送日志通知的服务器 **MUST** 在 `initialize` 的 capabilities 中声明 `logging`。
  SDK 自动声明（`"capabilities": { "logging": {} }`），并自动处理客户端的
  `logging/setLevel`（结果存在 `McpServer.LoggingLevel`）。
- 工具方法通过特殊参数 `McpServer server` 取得当前会话：SDK 自动注入该参数，
  且不会把它放进工具的 `inputSchema`。随后用
  `server.AsClientLoggerProvider().CreateLogger("w3strings")` 取到 `ILogger`。
- 级别映射（SDK 负责）：`Trace` 无对应 MCP 级别被丢弃，`Debug`→`debug`、`Information`→`info`、
  `Warning`→`warning`、`Error`→`error`、`Critical`→`critical`。低于客户端设定级别的事件不发出。
- `logger` 字段固定为 `w3strings`，便于客户端按组件过滤。
- 客户端必须先发 `logging/setLevel`：未设置时实测不发任何通知（规范亦说明此时服务器可自行决定）。

埋点（9 处）：

| 级别 | 工具 | 消息 |
| --- | --- | --- |
| `info` | `open_document` | `Opened {File} as document {Handle}` |
| `info` | `create_document` | `Created document {Handle}` |
| `info` | `save_document` | `Saved document {Handle} as version {Version} for {Language}`；未写盘时 `warning` |
| `info` | `close_document` | `Closed document {Handle} (saved before close: {SavedBeforeClose})` |
| `debug` | `add_item` | `Added item {StrId} to document {Handle}` |
| `debug` | `update_item` | `Updated item {StrId} of document {Handle}` |
| `debug` | `update_item_id` | `Changed item id {StrId} to {NewStrId} in document {Handle}` |
| `debug` | `update_item_key_name` | `Set the key name of item {StrId} in document {Handle}` |
| `debug` | `delete_item` | `Deleted item {StrId} from document {Handle}` |

**SDK 弃用说明**

- 2.3.0 把 `McpServer.AsClientLoggerProvider()` 标记为 `MCP9005` 弃用（SEP-2577：规范 2026-07-28
  起弃用 Logging）。项目面向 2025-11-25 且需要该能力，故在唯一调用点用
  `#pragma warning disable MCP9005` 局部抑制，并在注释中说明原因。

## 五、变更方案

### 5.1 文档说明

本文件是 `.trae/documents/` 下唯一的 W3Strings 工具文档，历史文档与临时计划文件均已删除。

### 5.2 返回模型

文件：`Witcher3StringEditor.MCP/Models/`（命名空间 `Witcher3StringEditor.MCP.Models`，每个模型一个文件）

- 不再需要专用返回模型：`navigate` 直接返回序列化器公开的 `StringItem?`，
  `W3StringsItemPage` / `W3StringsItemView` 均已删除。
- `W3StringsSearchResult`：`Handle`、`Query`、`IsCaseSensitive`、`MatchCount`、`Items`（全部命中）。
- `W3StringsJsonContext` 登记 `Guid`、`int`、`bool`、`string[]`、`W3StringsSearchResult`、`StringItem`
  与 `StringItem[]`；`StringItem` 已登记，可直接用于 `StringItem?` 返回值。

### 5.3 读取实现（已实现为扩展成员）

文件：`Witcher3StringEditor.MCP/Storage/DocumentStorage.Operations.cs`

```csharp
public StringItem? Navigate(string? strId, int offset)
{
    if (document.Items.Count == 0) return null;

    var index = string.IsNullOrWhiteSpace(strId)
        ? 0
        : document.Items.IndexOf(FindItem(document, NormalizeStrId(strId)));
    var target = index + offset;

    return target >= 0 && target < document.Items.Count ? Clone(document.Items[target]) : null;
}
```

实现要点：

1. 空文档直接返回 `null`；省略 `strId` 时基准索引为 `0`（第一条）。
2. 传入 `strId` 时先用 `NormalizeStrId` 规范化（`"01"` → `"1"`），再由 `FindItem` 定位，
   位置取条目在 `Items` 中的当前下标。
3. 目标下标 `index + offset` 越界（小于 `0` 或不小于 `Count`）返回 `null`，否则返回副本
   `Clone(document.Items[target])`。
4. `offset` 可正可负、可跨多条；不做排序、不做正则。
5. `SearchItems` 保持原实现：空查询校验 + 全字段子串匹配 + `Select(Clone).ToArray()`。

方法签名：

```csharp
public StringItem? Navigate(string? strId, int offset)

public W3StringsSearchResult SearchItems(string query, bool caseSensitive)
```

### 5.4 Handle 与文档状态的返回方式

文件：`Witcher3StringEditor.MCP/Storage/DocumentStorage.Lifecycle.cs` 与
`Witcher3StringEditor.MCP/Tools/W3StringsDocumentTools.cs`

- `OpenAsync` / `CreateAsync` 的返回类型由 `W3StringsDocumentInfo` 改为 `Guid`：
  重复打开同一路径时返回已登记的 Handle，新建文档时返回 `Guid.NewGuid()`。
- `save_document` 返回 `bool` 报告写盘是否成功（`SaveAsync` / `SaveCoreAsync` 返回 `Task<bool>`），
  不再删除返回值。
- 删除 `W3StringsDocumentInfo` 模型、`Info()` 实例扩展成员与它在 `W3StringsJsonContext` 中的登记；
  改为两个只读工具：

```csharp
[McpServerTool(Name = "count", ReadOnly = true)]
[Description("Returns the number of string items in the document.")]
public static Task<int> Count(Guid handle)
{
    return W3StringsDocument.ExecuteAsync(handle, document => document.Items.Count);
}

[McpServerTool(Name = "is_dirty", ReadOnly = true)]
[Description("Returns whether the document has unsaved changes: true after any item change or a new document, false after save_document.")]
public static Task<bool> IsDirty(Guid handle)
{
    return W3StringsDocument.ExecuteAsync(handle, document => document.IsDirty);
}
```

- `count` / `is_dirty` 都只接收 `handle`，直接读取内存文档，不返回条目数据。
- 未知 Handle → `KeyNotFoundException`（与其他工具一致）；关闭后再调用任何以 Handle 为参数的工具都会报此错误。

### 5.5 工具签名与描述

文件：`Witcher3StringEditor.MCP/Tools/W3StringsDocumentTools.cs`

```csharp
[McpServerTool(Name = "navigate", ReadOnly = true)]
[Description("Moves relative to an item and returns the item reached: offset 0 returns the reference item, positive offsets move forward, negative offsets move backward. Omit strId to start from the first item. Returns nothing when the target is outside the document.")]
public static Task<StringItem?> Navigate(
    [Description("Document handle returned by open_document or create_document.")]
    Guid handle,
    [Description("Number of items to move. 0 returns the reference item, positive moves forward, negative moves backward.")]
    int offset,
    [Description("32-bit unsigned decimal string id of the reference item. Omit it to start from the first item.")]
    string? strId = null)
{
    return W3StringsDocument.ExecuteAsync(handle, document => document.Navigate(strId, offset));
}

[McpServerTool(Name = "search_items", ReadOnly = true)]
[Description("Searches the string items of the document by substring in their string id, text or key name and returns all matches.")]
public static Task<W3StringsSearchResult> SearchItems(
    [Description("Document handle returned by open_document or create_document.")]
    Guid handle,
    [Description("Substring to search for. It is matched against the string id, text and key name of every item.")]
    string query,
    [Description("Match case sensitively. Defaults to false.")]
    bool caseSensitive = false)
{
    return W3StringsDocument.ExecuteAsync(handle,
        document => document.SearchItems(query, caseSensitive));
}
```

- `navigate`：`offset` 必填（正数向后、负数向前、`0` 返回基准），`strId` 省略即以第一条为基准；
  越界返回空 `content`，无 `limit`、无游标。
- `search_items`：无游标、无分页，一次返回全部命中。
- 其余 11 个工具不改；`open_document` / `create_document` 改为返回 `Guid`、`save_document` 返回 `bool`
  已见 5.4。

### 5.6 不修改的部分

- 序列化器的错误处理方式（`Serialize` 失败仍返回 `(false, null)`）。
- `Program.cs` 注册方式与 JSON 选项组合。
- `get_item`、`add_item`、`update_item`、`update_item_id`、`update_item_key_name`、`delete_item`、
  `close_document`、`list_languages` 的既有行为（`open_document` 只加载条目，
  文档不持有路径、语言或编码）。
- 临时验证脚本策略。

## 六、关键决策

### 原有决策（保持）

- 文档状态只存在于 MCP 进程内存；进程重启后 Handle 失效，需要重新 `open_document`。
- 显式保存：`add/update/delete/update_item_id/update_item_key_name` 不写盘，只有 `save_document` 或
  `close_document(saveBeforeClose=true)` 写盘。
- `create_document` 不接收参数，创建纯内存文档；`open_document` 只接收 `filePath`。
- 文档不持有路径、语言、编码；保存时由 `save_document` 的参数决定，`version` 必填。
- 从未保存的文档（`create_document`）保存时必须提供 `filePath`；`open_document` 打开的文档省略时使用打开时的路径。
- 同一路径同一进程只允许一个打开中的 Handle。
- `save_document` 覆盖目标文件，不自动创建父目录。
- 序列化失败只返回 `(false, null)`，工具层给出通用提示（重复/非法 `StrId`），不修改序列化器接口。
- 不新增测试项目。
- 搜索为全字段子串匹配：`strId`、`text`、`keyName` 任一包含即命中；默认忽略大小写。
- 搜索不做数值归一化；空查询报错；无命中时 `items` 为空数组；不搜索 `KeyHex`。
- `StrId` 可修改（`update_item_id`），新 id 必须合法且唯一。
- `keyName` 变化即清空 `keyHex`；`update_item` 同时传 `keyName` 与 `keyHex` 时显式 `keyHex` 生效。

### 本次新增/调整

- 删除 `list_items`、`previous_item`、`next_item` 三个工具，合并为单个 `navigate`：
  参数 `handle` + `offset` + 可选 `strId`，一次返回一条 `StringItem?`。
- `offset` 语义：`0` 返回基准条目，正数向后（下一条），负数向前（上一条）；省略 `strId` 时基准为第一条。
- 目标越界返回 `null`（MCP 响应为空 `content`）；非法或未知 `strId` 直接报错。
- 删除 `get_info`、`GetInfoAsync` 与 `W3StringsDocumentInfo`；`open_document` / `create_document`
  改为直接返回 `Guid`，`save_document` 返回 `bool`。
- 新增 `count`（返回 `int`）与 `is_dirty`（返回 `bool`）两个只读工具，用于查询文档状态。
- `search_items` 返回全部命中（`items`）与 `matchCount`，不接受游标。
- 排序保持文档原始顺序；`search_items` 也按文档顺序返回命中项。
- 这是返回结构的破坏性变更：`W3StringsItemPage`、`W3StringsItemView`、`W3StringsDocumentInfo` 均已退役，
  工具总数由 16 个变为 15 个。

## 七、实施顺序

1. 模型：删除 `W3StringsDocumentInfo`，确认 `W3StringsItemPage` / `W3StringsItemView` 已删除；
   `W3StringsJsonContext` 改登记 `Guid` / `int` / `bool` / `string[]` / `W3StringsSearchResult` /
   `StringItem` / `StringItem[]`（5.2、5.4）。
2. 扩展成员：删除 `ListItems` / `PreviousItem` / `NextItem`，新增 `Navigate(strId, offset)`；
   `SearchItems` 返回全部命中；删除 `Info()`（5.3、5.4）。
3. 共享辅助：删除仅供旧的逐条读取使用的 `ResolveIndex`（5.3）。
4. 生命周期：`OpenAsync` / `CreateAsync` 返回 `Guid`，`SaveAsync` / `SaveCoreAsync` 返回 `Task<bool>`（5.4）。
5. 工具：删除 `list_items` / `previous_item` / `next_item` / `get_info`，新增 `navigate`、`count`、
   `is_dirty`；`open_document` / `create_document` 返回 `Guid`，`save_document` 返回 `bool`（5.4、5.5）。
6. 编译：`dotnet build Witcher3StringEditor.MCP.slnx`，预期 0 error 0 warning。
7. stdio 读取端到端与错误路径验证（见第八章），清理临时脚本与临时文件。
8. Native AOT 发布冒烟。
9. 文档：保持本文件为唯一文档，历史文档与临时计划文件已删除。

## 八、验证步骤

1. 编译：`dotnet build Witcher3StringEditor.MCP.slnx`，预期 0 error 0 warning。
2. stdio 端到端（`%TEMP%` 下临时 PowerShell 脚本，完成后删除）：
   - `tools/list` 返回 15 个工具；`count` 与 `is_dirty` 的 schema 均为 `handle`，
     `navigate` 为 `handle` / `offset` / `strId`，`search_items` 为 `handle` / `query` / `caseSensitive`，
     `save_document` 为 `handle` / `version` / `filePath` / `language`；
     `list_items` / `previous_item` / `next_item` / `get_info` 均不再出现。
   - 生命周期：`create_document()` 返回裸 GUID 字符串（无包装字段），`count=0`、`is_dirty=true`；
     添加条目后 `count` 递增、`is_dirty` 仍为 `true`；`save_document(version, filePath, language)`
     返回 `true` 且目标文件存在，`is_dirty` 变为 `false`；`open_document(filePath)` 返回新的裸 GUID，
     且 `count` 正确、`is_dirty=false`；未知 Handle 返回工具错误；`get_info` 调用返回"工具不存在"错误。
     `version` 必填，接受 `162` / `v162` / `163` / `v163`（UTF-16LE，写盘为 162）与 `164` / `v164`（UTF-8）；
     省略 `version` 返回工具错误，且不产生文件。
   - `create_document` 的文档未提供 `filePath` 直接保存：返回工具错误而不是崩溃。
   - 新建文档并添加 `strId` 为 `"1"`…`"5"` 的条目：`text` 为 `"item 1"`…`"item 4"` 与 `"other"`。
   - 空文档 `navigate(offset=0)`：返回空 `content`（等价 `null`）。
   - `navigate(offset=0)` 省略 `strId`：返回 `strId="1"` 的 `StringItem`，不含任何包装键。
   - `navigate(offset=2)` 省略 `strId`：返回 `strId="3"`。
   - `navigate(strId="1", offset=0)` → `"1"`；`offset=1` → `"2"`；`offset=-1`（strId="3"）→ `"2"`；
     `offset=4`（strId="1"）→ `"5"`；`offset=-4`（strId="5"）→ `"1"`。
   - 越界：`navigate(strId="1", offset=-1)`、`navigate(strId="5", offset=1)`、
     `navigate(offset=-2)`（省略 strId）、`navigate(strId="1", offset=99)` 均返回空 `content`。
   - `search_items(query="item")`：键为 `handle,query,isCaseSensitive,matchCount,items`；
     `matchCount=4`、`items.strId` 依次为 `1,2,3,4`。
   - `search_items(query="ITEM", caseSensitive=true)`：`matchCount=0`、`items=[]`、`isCaseSensitive=true`。
   - 错误路径：未知 `strId`（如 `"99"`）、非法 `strId`（如 `"abc"`）、空 `query`
     均返回工具错误而非进程崩溃。
   - 删除条目后，`count` 减一、`navigate(strId="1", offset=1)` 跳过被删除项；同一 `query` 的 `matchCount` 减一。
   - 基线回归：`get_item`、`add_item`、`update_item`、`update_item_id`、`update_item_key_name`、
     `delete_item`、`close_document`、`list_languages` 行为不变；round-trip 后条目数量与 key hash 正确。
3. Native AOT：`dotnet publish Witcher3StringEditor.MCP/Witcher3StringEditor.MCP.csproj -c Release -r win-x64`，
   确认 `Guid` / `int` / `bool` 返回值、`StringItem?` 与 `items` 数组在 AOT 下正常序列化，
   发布产物冒烟测试通过。
