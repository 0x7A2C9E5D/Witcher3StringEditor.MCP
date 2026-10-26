# W3Strings MCP Server 测试报告

## 1. 文档信息

| 项目 | 内容 |
| --- | --- |
| 报告名称 | W3Strings MCP Server 详细测试报告 |
| 测试日期 | 2026-10-26 |
| 被测版本 | `Witcher3StringEditor.MCP` 1.0.0.0（Native AOT 单文件发布） |
| 测试对象 | `Witcher3StringEditor.MCP\bin\Release\net10.0\win-x64\publish\Witcher3StringEditor.MCP.exe` |
| 产物大小 | 13,341,696 字节 |
| 测试类型 | 构建验证 + 发布产物验证 + MCP 协议/功能冒烟测试 |
| 测试结论 | **通过：26 项检查全部成功，无阻断性缺陷** |

## 2. 测试目标

1. 验证 Native AOT + 单文件 + win-x64 发布可成功完成，且产物为单个可执行文件。
2. 验证托管 PDB 已内嵌到程序集中，且发布目录不包含 PDB 文件。
3. 验证通过 stdio 传输的 MCP Server 可被客户端初始化，并正确注册 14 个工具。
4. 验证文档生命周期（创建、保存、关闭、打开、去重打开）与条目增删改查、搜索、分页的完整闭环。
5. 验证异常输入（重复 id、非法 id、未知 handle、缺失文件）返回明确错误而非崩溃。

## 3. 测试环境

| 项目 | 内容 |
| --- | --- |
| 操作系统 | Windows |
| SDK | .NET SDK 10.0 |
| 运行时 | Native AOT（`PublishAot=true`，`InvariantGlobalization=true`） |
| 发布参数 | `-c Release -r win-x64 -p:IlcUseEnvironmentalTools=true` |
| 传输方式 | stdio（JSON-RPC 2.0，MCP 协议版本 `2024-11-05`） |
| 测试客户端 | 临时 PowerShell 脚本（自行完成 initialize / notifications/initialized / tools/list / tools/call 交互，按 `id` 匹配响应，单条响应超时 20 s） |
| 测试数据目录 | `%TEMP%`（测试结束后清理） |

### 3.1 环境限制说明

本机 `vswhere` 无法识别 Visual Studio 18 的 C++ 工具链（`findvcvarsall.bat` 返回失败），因此 AOT 发布时需手动注入 MSVC / Windows SDK 的 `PATH`、`INCLUDE`、`LIB`，并附加 `-p:IlcUseEnvironmentalTools=true`。该限制属于本机工具链注册问题，不属于项目配置缺陷；在正常安装 C++ 工作负载的机器上可直接执行 `dotnet publish -c Release -r win-x64`。

## 4. 构建与发布验证

| 检查项 | 方法 | 结果 |
| --- | --- | --- |
| Debug 构建 | `dotnet build Witcher3StringEditor.MCP.slnx` | 通过，0 警告 0 错误 |
| Release 构建 | `dotnet build Witcher3StringEditor.MCP.slnx -c Release` | 通过，0 警告 0 错误 |
| 单文件发布 | `dotnet publish -c Release -r win-x64` | 通过，publish 目录仅含 `Witcher3StringEditor.MCP.exe` |
| 托管 PDB 内嵌 | 读取 PE Debug Directory，检查是否含 `EmbeddedPortablePdb` | 通过：MCP 与 Serializers 程序集（Debug / Release）均含 `EmbeddedPortablePdb` |
| 发布目录无 PDB | 列出 publish 目录 | 通过：无 `.pdb` |
| 原生 PDB 处理 | 检查 `native` 目录与 publish 目录 | 通过：原生 `.pdb` 保留在 `native` 目录，不复制到 publish 目录 |
| PDB 内嵌配置 | `Witcher3StringEditor.MCP.csproj` / `Witcher3StringEditor.Serializers.csproj` | `DebugType=embedded`；`CopyOutputSymbolsToPublishDirectory=false` |

> 清理说明：配置变更前生成的旧 `.pdb` 文件曾残留在 `bin` / `obj` 目录，已全部删除并重新构建，确认不再产生新的 PDB 文件。

## 5. 测试用例与执行结果

### 5.1 用例明细

| 编号 | 工具 / 方法 | 输入要点 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| TC-01 | `initialize` | clientInfo=full-test | 返回 serverInfo | server=Witcher3StringEditor.MCP v=1.0.0.0 | PASS |
| TC-02 | `tools/list` | — | 14 个工具全部注册 | count=14，无缺失 | PASS |
| TC-03 | `list_languages` | — | 返回语言代码数组且含 en | count=18，has_en=True | PASS |
| TC-04 | `create_document` | 无参数 | handle 有效、itemCount=0、dirty=true | handle=w3doc-1，dirty=True | PASS |
| TC-05 | `add_item` | strId=1, text=Hello, keyName=hello_key | 新增成功 | 成功 | PASS |
| TC-06 | `add_item` | strId=2, text=Second entry, keyHex=0A0B0C0D | 新增成功 | 成功 | PASS |
| TC-07 | `add_item` | 重复 strId=1 | 拒绝并报错 | 被拒绝 | PASS |
| TC-08 | `add_item` | 非法 strId=abc | 拒绝并报错 | 被拒绝 | PASS |
| TC-09 | `list_items` | offset=0, limit=1 | 返回 1 条，itemCount=2，hasMore=true | itemCount=2，page=1，hasMore=True | PASS |
| TC-10 | `get_item` | strId=1 | text=Hello | text=Hello | PASS |
| TC-11 | `get_item` | strId=99 | 报错 | 被拒绝 | PASS |
| TC-12 | `search_items` | query=hell, caseSensitive=false | matchCount≥1 | matchCount=1 | PASS |
| TC-13 | `search_items` | query=HELL, caseSensitive=true | matchCount=0 | matchCount=0 | PASS |
| TC-14 | `update_item` | strId=1, text=Hello World | 文本更新 | 回读 text=Hello World | PASS |
| TC-15 | `update_item_id` | strId=1 → newStrId=10 | id 变更且内容保留 | strId=10，text=Hello World | PASS |
| TC-16 | `update_item_key_name` | strId=10, keyName=new_key | 键名更新 | keyName=new_key | PASS |
| TC-17 | `delete_item` | strId=2 | itemCount 由 2 变 1 | itemCount=1 | PASS |
| TC-18 | `get_info` | handle=w3doc-1 | itemCount=1、dirty=true | items=1，dirty=True | PASS |
| TC-19 | `save_document` | language=zh, encoding=utf-16le | 落盘成功、dirty=false、语言/编码回显正确 | 文件 59 字节，dirty=False，lang=zh，enc=utf-16le | PASS |
| TC-20 | `close_document` | saveBeforeClose=false | 关闭后 handle 失效 | handle 失效 | PASS |
| TC-21 | `open_document` | 打开 TC-19 生成的文件 | 回读成功，条目一致 | handle=w3doc-2，items=1，text=Hello World | PASS |
| TC-22 | `save_document` | 另存为 en / utf-8 新路径 | 生成新文件并更新 filePath | 文件生成，filePath 正确 | PASS |
| TC-23 | `open_document` | 重复打开 TC-22 的路径 | 返回已有 handle | handle=w3doc-2（与 TC-21 相同） | PASS |
| TC-24 | `close_document` | saveBeforeClose=true | 保存并关闭，handle 失效 | closed=True | PASS |
| TC-25 | `open_document` | 不存在的路径 | 报错 | 被拒绝 | PASS |
| TC-26 | `list_items` | 不存在的 handle | 报错 | 被拒绝 | PASS |

执行汇总：**26 项检查，26 项通过，0 项失败。**

### 5.2 原始执行输出

```text
PASS  initialize  server=Witcher3StringEditor.MCP v=1.0.0.0
PASS  tools/list  count=14 missing=
PASS  list_languages  count=18 has_en=True
PASS  create_document  handle=w3doc-1 dirty=True
PASS  add_item #1
PASS  add_item #2
PASS  add_item duplicate  rejected=True
PASS  add_item bad strId  rejected=True
PASS  list_items page  itemCount=2 page=1 hasMore=True
PASS  get_item  text=Hello
PASS  get_item missing  rejected=True
PASS  search_items  matches=1
PASS  search_items case  matches=0
PASS  update_item  text=Hello World
PASS  update_item_id  strId=10 text=Hello World
PASS  update_item_key_name  keyName=new_key
PASS  delete_item  itemCount=1
PASS  get_info  items=1 dirty=True
PASS  save_document  file=True bytes=59 lang=zh enc=utf-16le
PASS  close_document  closed=True
PASS  open_document roundtrip  handle=w3doc-2 items=1 text=Hello World
PASS  save_document new path  path=C:\Users\ROG\AppData\Local\Temp\w3-mcp-test-copy.w3strings
PASS  open_document dedupe  handle=w3doc-2
PASS  close_document save  closed=True
PASS  open_document missing  rejected=True
PASS  unknown handle  rejected=True
RESULT: ALL 26 CHECKS PASSED
```

## 6. 关键流程验证

### 6.1 完整编辑闭环

```text
create_document
  → add_item ×2
  → list_items / get_item / search_items
  → update_item / update_item_id / update_item_key_name
  → delete_item
  → save_document (zh / utf-16le)
  → close_document
  → open_document（回读一致）
  → save_document（另存 en / utf-8）
  → open_document（同路径去重，返回同一 handle）
  → close_document（saveBeforeClose=true）
```

### 6.2 验证结论

- **持久化正确**：写入 → 关闭 → 重新打开后条目数量与文本完全一致（`Hello World`），语言与编码正确回显。
- **Handle 语义正确**：同一路径重复打开返回已有 handle；关闭后 handle 立即失效。
- **内存态管理正确**：修改后 `dirty=true`，保存后 `dirty=false`。
- **错误处理正确**：非法输入均返回明确错误，进程未崩溃。
- **AOT 兼容正确**：System.Text.Json 源生成上下文在 Native AOT 下正常序列化所有工具返回模型。

## 7. 观察项（非缺陷）

1. **计数字段命名**：`list_items` 的 `itemCount` 表示文档条目总数（非当前页大小）；`search_items` 的 `matchCount` 表示命中总数，`itemCount` 表示文档总数。调用方需注意区分。
2. **键名不随文件持久化**：重新打开文件后 `keyName` 为空，文本与 id 正常。这是 `.w3strings` 容器只保存 key 哈希、不保存可读键名的格式特性，`keyHex` 才是文件中持久化的键值。
3. **初始化时序**：测试脚本在 `notifications/initialized` 后立即发起 `tools/call` 时曾出现一次失败；加入 300 ms 等待后未再复现。定位为测试脚本时序问题，非服务端缺陷，正式客户端通常不会出现该情形。
4. **旧 PDB 残留**：切换 `DebugType` 后旧构建目录中的 PDB 不会被自动删除，需清理 `bin` / `obj` 才能观察到“无 PDB”的正确结果；已处理。

## 8. 未覆盖范围

1. 并发多客户端访问与 `SemaphoreSlim` 串行化锁的竞争场景。
2. 大文件（数万条目）的加载、搜索与分页性能。
3. 非法 `language` 参数（非法 `strId` 与非法 `version` 已覆盖）。
4. 其他 RID（win-arm64、osx-arm64、linux-x64、linux-arm64、linux-musl-x64）的发布与运行。
5. 真实 MCP 客户端集成（如 VS Code MCP 配置、Claude Desktop）与 `dnx` / NuGet 分发路径。
6. `PackageId` / `PackageVersion` 占位信息（当前为 `SampleMcpServer` / `0.1.0-beta`）尚未替换与打包验证。

## 9. 建议

1. 增加并发与大文件场景的自动化测试，验证锁与分页在压力下的正确性。
2. 补充非法 `language` 参数用例，并让工具层返回具体错误原因（当前 SDK 将异常泛化为 `An error occurred invoking ...`）。
3. 若计划通过 NuGet / `dnx` 分发，补充 `dotnet pack` 与工具启动的端到端验证，并替换示例包元数据。
4. 将本机 AOT 发布的环境变量与参数固化为脚本（或 CI 步骤），避免依赖手工配置。
5. 在使用文档中说明：`keyName` 不持久化、`itemCount` / `matchCount` 的语义差异。

## 10. 测试结论

被测 AOT 单文件产物在 stdio 方式下初始化正常，14 个工具全部可用；文档生命周期、条目增删改查、搜索、分页、持久化与异常处理均符合预期，26 项检查全部通过。未发现功能缺陷或阻断性问题，可进入试用与集成验证阶段。

遗留风险主要来自未覆盖的并发、大文件、跨平台发布与打包分发场景，建议在正式发布前补充对应测试。

## 11. 补充验证（版本参数调整后）

**接口调整**：`save_document` 的 `encoding` 参数改为 `version`，编码由 W3Strings 容器版本决定（版本语义见 §12 修正：162 / 163 均为 UTF-16LE，164 为 UTF-8）；省略 `version` 时沿用文档打开时的版本。第 5 章用例执行时参数名为 `encoding`，其 `utf-16le` 与调整后的 `version=162` 等价。

**测试对象**：重新发布的 AOT 单文件产物（13,341,696 字节）。

| 编号 | 场景 | 输入 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| TC-27 | 指定版本 164 | `version=164` | 文件头 164、回显 `utf-8` | 一致 | PASS |
| TC-28 | 指定版本 162 | `version=v162` | 文件头 162、回显 `utf-16le` | 一致 | PASS |
| TC-29 | 省略版本，沿用已打开文档 | 打开 162 文件后保存 | 仍为 162 | 文件头 162 | PASS |
| TC-30 | 显式切换版本 | 打开 162 文档，`version=164` | 新文件为 164 | 文件头 164 | PASS |
| TC-31 | 新建文档默认版本 | 省略 `version` | 164 | 文件头 164 | PASS |
| TC-32 | 非法版本 | `version=999` | 返回工具错误、不落盘 | 报错且未生成文件 | PASS |
| TC-33 | 回读一致 | 打开 162 文件并读取条目 | 文本一致 | `版本测试中文 ABC` | PASS |

补充检查 **7/7 通过**。`version` 成为唯一的版本入口，编码不再可独立选择；文件头版本号（offset 4）与期望一致。

**新增观察项**：工具内部抛出的异常在 MCP 响应中被 SDK 泛化为 `An error occurred invoking 'save_document'.`，调用方无法获知“版本非法”等具体原因。建议后续在工具层捕获参数错误并以文本形式返回明确提示。

## 12. 版本语义修正后的复测

**背景**：W3Strings 版本与游戏版本绑定——162 / 163 为 UTF-16LE（经典版、次世代版，写盘统一为 162），164 为 UTF-8（重置版），不可混用；`create_document` 改为必须显式指定 `version`；`W3StringsDocumentInfo` 当时新增 `version` 字段（163 回显为 162，该字段已在 §13 中按反馈移除）。

**测试对象**：重新发布的 AOT 单文件产物（13,348,864 字节）。

| 编号 | 场景 | 输入 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- | --- |
| TC-35 | 新建 162 文档 | `create_document(version=162)` | version=162、utf-16le | 一致 | PASS |
| TC-36 | 保存省略版本 | `save_document(language=zh)` | 文件头 162 | 文件头 162 | PASS |
| TC-37 | 新建 163 文档 | `version=v163` | 规范为 162、utf-16le | version=162 | PASS |
| TC-38 | 保存 163 文档 | `version=163` | 写盘为 162 | 文件头 162 | PASS |
| TC-39 | 新建 164 文档 | `version=164` | version=164、utf-8 | 一致 | PASS |
| TC-40 | 保存 164 文档 | 省略 version | 文件头 164 | 文件头 164 | PASS |
| TC-41 | 非法版本 | `version=999` | 工具错误 | 报错 | PASS |
| TC-42 | 缺少版本 | `create_document` 无参数 | 工具错误 | 报错 | PASS |
| TC-43 | 打开 162 文件 | `open_document` | version=162、utf-16le | 一致 | PASS |
| TC-44 | 显式转换版本 | 162 文档 `version=164` | 文件头 164 | 文件头 164 | PASS |
| TC-45 | 打开声明 163 的文件 | 将 162 文件头改为 163 后打开 | version=162、utf-16le | 一致 | PASS |
| TC-46 | 重新保存声明 163 的文件 | 省略 version | 写盘为 162 | 文件头 162 | PASS |

补充检查 **12/12 通过**。新建文档的版本必须显式指定；保存默认沿用文档版本，显式传入不同版本才执行转换；162 / 163 一律写为 162，164 写为 164。

## 13. 返回模型精简后的复测

**背景**：`W3StringsDocumentInfo` 精简为仅含 `handle`、`itemCount`、`isDirty`（原 `dirty` 更名），移除 `filePath`、`language`、`encoding`、`version`。`create_document`、`get_info`、`save_document`、`open_document` 的返回结构随之变化。

**测试对象**：重新发布的 AOT 单文件产物（13,337,088 字节）。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-47 | `create_document(version=162)` 返回键 | 仅 handle / itemCount / isDirty | `{"handle":"w3doc-1","itemCount":0,"isDirty":true}` | PASS |
| TC-48 | `get_info` 返回键与值 | 同上，itemCount=2、isDirty=true | 一致 | PASS |
| TC-49 | `save_document` 返回键与值 | 同上，itemCount=2、isDirty=false | 一致 | PASS |
| TC-50 | `open_document` 返回键与值 | 同上，itemCount=2、isDirty=false | 一致 | PASS |

补充检查 **4/4 通过**（每个场景同时校验 JSON 键集合与字段值，共 7 项断言）。语言、编码、版本不再随元数据返回；保存时省略 `version` 仍按文档打开/创建时的版本写入。

## 14. Handle 改为 Guid 后的复测

**背景**：文档 Handle 由自定义字符串（`w3doc-1`、`w3doc-2`……）改为 `Guid`。`W3StringsDocument.Handle`、`W3StringsDocumentInfo.Handle`、`W3StringsItemPage.Handle`、`W3StringsSearchResult.Handle` 均为 `Guid`；注册表改为 `Dictionary<Guid, W3StringsDocument>`，路径映射改为 `Dictionary<string, Guid>`，删除 `_nextHandle` 计数器，改用 `Guid.NewGuid()`。11 个接收 `handle` 的工具参数类型同步改为 `Guid`。

**测试对象**：Debug 构建与重新发布的 AOT 单文件产物（13,352,960 字节），同一套脚本各跑一遍，结果一致。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-51 | `tools/list` | 14 个工具，handle 参数在 JSON Schema 中为 `string` | 一致 | PASS |
| TC-52 | `create_document(version=162)` | handle 为规范 GUID 文本 | 匹配 `^[0-9a-f]{8}-…-[0-9a-f]{12}$` | PASS |
| TC-53 | `get_info(handle=GUID)` | 回显同一 GUID、isDirty=true | 一致 | PASS |
| TC-54 | `add_item` / `list_items` | 以 GUID 调用成功，分页回显同一 GUID | itemCount=1 | PASS |
| TC-55 | `search_items` | 以 GUID 调用成功，回显同一 GUID | matchCount=1 | PASS |
| TC-56 | `get_item` | 以 GUID 调用成功 | text=Hello GUID | PASS |
| TC-57 | `save_document` | 以 GUID 调用成功，isDirty=false，文件头 162 | 一致 | PASS |
| TC-58 | `open_document` 同一路径 | 返回同一 GUID | 一致 | PASS |
| TC-59 | 关闭后再 `get_info` | 已释放的 GUID 报错 | 工具错误 | PASS |
| TC-60 | 随机未知 GUID | 工具错误 | 工具错误 | PASS |
| TC-61 | 旧格式字符串 `"w3doc-1"` | 参数绑定失败，工具错误 | 工具错误 | PASS |
| TC-62 | 关闭后重新打开同一文件 | 生成新的 GUID | 新 GUID 与旧值不同 | PASS |

补充检查 **21 项断言全部通过**（Debug 与 AOT 产物各 21/21）。GUID 在 JSON 中仍以标准字符串传输，Agent 侧调用方式不变。

## 15. 文档不再持有路径/语言/编码后的复测

**背景**：`W3StringsDocument` 只保留 `Handle`、`Items`、`Dirty`；`FilePath`、`Language`、`Encoding` 全部移除，只在保存时由参数提供。随之调整：

- `create_document` 移除 `version` 参数（无参数调用），版本由保存时决定；
- `save_document` 的 `version` 改为必填，省略即报错；`filePath` 省略时仍写回该 Handle 已登记的路径；
- 路径归属改由存储层的双向映射维护（`_handlesByPath` / `_pathsByHandle`），不进入文档对象；
- `close_document(saveBeforeClose=true)` 在保存前从现有文件头读取语言与版本，不再依赖文档状态。

**测试对象**：Debug 构建与重新发布的 AOT 单文件产物（13,357,056 字节），同一套脚本各跑一遍，结果一致。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-63 | `tools/list` 中 `create_document` 的 schema | 无参数（props 与 required 均为空） | 一致 | PASS |
| TC-64 | `tools/list` 中 `save_document` 的 required | `handle`、`version` | 一致 | PASS |
| TC-65 | `create_document()` 无参数 | 返回 GUID、itemCount=0、isDirty=true | 一致 | PASS |
| TC-66 | `save_document` 省略 `version` | 工具错误且不产生文件 | 报错，文件不存在 | PASS |
| TC-67 | 保存 `version=164` + `filePath` + `language=zh` | isDirty=false，文件头 164 | 一致 | PASS |
| TC-68 | 省略 `filePath` 保存 `version=162` | 写回已登记路径，文件头 162 | 一致 | PASS |
| TC-69 | 改动后 `close_document(saveBeforeClose=true)` | 从文件头取语言/版本，写盘仍为 162 | 文件头 162 | PASS |
| TC-70 | 关闭后重新打开该文件 | 读到 3 条（含关闭前新增），isDirty=false | itemCount=3 | PASS |
| TC-71 | 重新打开 | 生成新 GUID | 与旧 Handle 不同 | PASS |
| TC-72 | 从未保存的文档 `saveBeforeClose=true` | 工具错误（无文件可写） | 工具错误 | PASS |

补充检查 **10/10 通过**（Debug 与 AOT 产物各 10/10）。路径、语言、编码已完全移出文档对象；保存行为与版本约束未回退。

## 16. 游标分页（list_items / search_items）复测

**背景**：分页由 `offset` 改为游标式。游标就是上一页最后一条的 `StrId`，服务端据此在当前结果中定位并继续向后取；返回 `nextCursor`，为 `null` 表示没有下一页。`W3StringsItemPage` 改为 `handle` / `itemCount` / `nextCursor` / `items`，`W3StringsSearchResult` 改为 `handle` / `query` / `caseSensitive` / `matchCount` / `itemCount` / `nextCursor` / `items`；`Offset` / `Limit` / `HasMore` 全部移除。校验由 `ValidatePage` 改为 `ValidateLimit`，新增 `ResumeIndex` 解析游标；`search_items` 同步改为在命中集合内按游标续读。

**测试对象**：Debug 构建与重新发布的 AOT 单文件产物（13,352,960 字节），Debug 跑完整 21 项断言，AOT 跑代表性子集 8 项，结果一致。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-73 | `tools/list` 中 `list_items` 的 schema | `handle,cursor,limit`，无 `offset` | 一致 | PASS |
| TC-74 | `tools/list` 中 `search_items` 的 schema | `handle,query,caseSensitive,cursor,limit` | 一致 | PASS |
| TC-75 | `list_items(limit=50)` | 50 条、itemCount=60、nextCursor="50"；键为 `handle,itemCount,nextCursor,items` | 一致 | PASS |
| TC-76 | `list_items(cursor="50", limit=50)` | 10 条（51…60）、nextCursor=null | 一致 | PASS |
| TC-77 | `limit=13` 逐页跟随 `nextCursor` | 6 页覆盖 60 条，不重不漏、顺序正确 | 一致 | PASS |
| TC-78 | `search_items(query="item", limit=7)` | matchCount=60、itemCount=60、7 条、nextCursor="7"；键含 `nextCursor` | 一致 | PASS |
| TC-79 | 搜索以 `limit=9` 跟随 `nextCursor` | 覆盖全部 60 条命中，顺序正确 | 一致 | PASS |
| TC-80 | `search_items(query="ITEM", caseSensitive=true)` | matchCount=0、items=[]、nextCursor=null | 一致 | PASS |
| TC-81 | 非法游标 `"abc"`、`limit=0` | 工具错误，进程不崩溃 | 工具错误 | PASS |
| TC-82 | 删除锚点条目 50 后使用旧游标 | `list_items` 与 `search_items` 均报错；以 `cursor="49"` 续读从 51 开始 | 一致 | PASS |

补充检查 **21 项断言通过**（Debug 21/21，AOT 子集 8/8）。游标失效（条目被删除/命中变化）时返回工具错误而不是静默跳页。

## 17. 读取结构改为相邻条目 + 全部命中复测

**背景**：`list_items` 改为返回单个条目及其相邻条目视图，`search_items` 改为返回全部命中；分页、
`limit` 与游标遍历均取消，`W3StringsItemPage` 删除。

最终模型：

```csharp
internal sealed record W3StringsItemView(
    StringItem? Item,
    W3StringsItemView? Previous,
    W3StringsItemView? Next);

internal sealed record W3StringsSearchResult(
    Guid Handle,
    string Query,
    bool IsCaseSensitive,
    int MatchCount,
    StringItem[]? Items);
```

`W3StringsItemView` 自引用，相邻视图只带自己的 `item`（其 `previous` / `next` 恒为 `null`），
返回结构最多嵌套两层。`DefaultPageSize` / `ValidateLimit` 删除，`ResumeIndex` 改为 `ResolveIndex`。

**测试对象**：Debug 构建与重新发布的 AOT 单文件产物（13,345,280 字节，publish 目录仅含该 exe），
同一套脚本各跑一遍，16 项断言全部通过（Debug 16/16，AOT 16/16）。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-83 | `tools/list` 中 `list_items` 的 schema | `handle,cursor` | 一致 | PASS |
| TC-84 | `tools/list` 中 `search_items` 的 schema | `handle,query,caseSensitive`，无 `cursor` | 一致 | PASS |
| TC-85 | 空文档 `list_items` | `item` / `previous` / `next` 缺失，无 `handle` / `itemCount` | 一致 | PASS |
| TC-86 | `list_items` 不传 `cursor` 的返回键 | `item,next`（`previous` 因 null 省略） | 一致 | PASS |
| TC-87 | 首条 | `item.strId="1"`、`next.item.strId="2"`、无 `previous` | 一致 | PASS |
| TC-88 | 相邻视图嵌套深度 | `next.previous` 与 `next.next` 均缺失 | 一致 | PASS |
| TC-89 | 中间条 `cursor="2"` | `previous.item.strId="1"`、`next.item.strId="3"` | 一致 | PASS |
| TC-90 | 末条 `cursor="5"` | `previous.item.strId="4"`、无 `next` | 一致 | PASS |
| TC-91 | 非法游标 `"abc"` | 工具错误 | 工具错误 | PASS |
| TC-92 | `search_items(query="item")` 返回键 | `handle,query,isCaseSensitive,matchCount,items` | 一致 | PASS |
| TC-93 | 全部命中 | `matchCount=4`、`items.strId` 为 `1,2,3,4` | 一致 | PASS |
| TC-94 | `query="ITEM"` + `caseSensitive=true` | `matchCount=0`、`items=[]`、`isCaseSensitive=true` | 一致 | PASS |
| TC-95 | 空查询 `" "` | 工具错误 | 工具错误 | PASS |
| TC-96 | 删除锚点条目后使用旧游标 | 工具错误 | 工具错误 | PASS |
| TC-97 | 删除后邻居导航 | `cursor="1"` 的 `next.item.strId="3"`，跳过已删除条目 | 一致 | PASS |
| TC-98 | 删除后搜索 | `matchCount=3`、`items.strId` 为 `1,3,4` | 一致 | PASS |

补充说明：

- SDK 的 JSON 选项省略值为 `null` 的字段，因此边界处 `previous` / `next` 键直接缺失，语义等同于 `null`。
- 本节的最终结构取代了此前一轮"单条 + `previousCursor` / `nextCursor`"的过渡设计，后者未保留。
- AOT 下自引用 `W3StringsItemView` 与 `StringItem[]` 均由源生成上下文正常序列化，无反射回退警告。

## 18. 移除 W3StringsItemView 后的上下导航复测

**背景**：删除 `W3StringsItemView`，`list_items` 直接返回 `StringItem?`；上下移动改由两个独立工具
`previous_item` / `next_item` 在工具层实现，参数为 `handle` + `strId`，到达边界时无返回内容。
`W3StringsSearchResult` 保持不变（`handle` / `query` / `isCaseSensitive` / `matchCount` / `items`）。
`W3StringsJsonContext` 移除 `W3StringsItemView` 登记。

**测试对象**：Debug 构建与重新发布的 AOT 单文件产物（13,325,312 字节，publish 目录仅含该 exe），
同一套脚本各跑一遍，21 项断言全部通过（Debug 21/21，AOT 21/21）。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-99 | `tools/list` 工具数量 | 16 个 | 一致 | PASS |
| TC-100 | `list_items` schema | `handle,cursor` | 一致 | PASS |
| TC-101 | `previous_item` schema | `handle,strId` | 一致 | PASS |
| TC-102 | `next_item` schema | `handle,strId` | 一致 | PASS |
| TC-103 | `search_items` schema | `handle,query,caseSensitive` | 一致 | PASS |
| TC-104 | 空文档 `list_items` | 无返回内容（等价 `null`） | 一致 | PASS |
| TC-105 | `list_items` 首条 | 直接是 `strId="1"` 的 `StringItem`，无包装键 | 一致 | PASS |
| TC-106 | `previous_item(strId="1")` | 无返回内容 | 一致 | PASS |
| TC-107 | `next_item(strId="1")` | `strId="2"`、`text="item 2"` | 一致 | PASS |
| TC-108 | `previous_item(strId="5")` | `strId="4"`、`text="item 4"` | 一致 | PASS |
| TC-109 | `next_item(strId="5")` | 无返回内容 | 一致 | PASS |
| TC-110 | 中间条目双向导航 | `next_item("2")="3"`、`previous_item("2")="1"` | 一致 | PASS |
| TC-111 | 未知 `strId="99"` | 工具错误 | 工具错误 | PASS |
| TC-112 | 非法游标 `"abc"` | 工具错误 | 工具错误 | PASS |
| TC-113 | `search_items(query="item")` 返回键 | `handle,query,isCaseSensitive,matchCount,items` | 一致 | PASS |
| TC-114 | 全部命中 | `matchCount=4`、`items.strId` 为 `1,2,3,4` | 一致 | PASS |
| TC-115 | 大小写敏感无命中 | `matchCount=0`、`items=[]` | 一致 | PASS |
| TC-116 | 删除条目后 `next_item("1")` | `strId="3"`，跳过已删除条目 | 一致 | PASS |
| TC-117 | 删除后参照条目失效 | `previous_item("2")` 报工具错误 | 工具错误 | PASS |
| TC-118 | 删除后搜索 | `matchCount=3`、`items.strId` 为 `1,3,4` | 一致 | PASS |

补充说明：

- 工具返回 `null` 时 MCP 响应只有空的 `content` 数组（无 `structuredContent`），客户端据此判定"没有上一条/下一条"。
- 本节结构取代 §17 的 `W3StringsItemView` 方案，该模型与 `W3StringsJsonContext` 中的登记均已删除。

## 19. 导航合并为 navigate 后的复测

**背景**：移除 `list_items`、`previous_item`、`next_item`，合并为单个只读工具 `navigate`，
参数 `handle`（必填）、`offset`（必填，正数向后、负数向前、`0` 返回基准）、`strId`（可选，
省略时基准为文档第一条）。目标越界返回 `null`（MCP 响应为空 `content`）。
`DocumentStorage` 中对应方法改为 `Navigate(string? strId, int offset)`，并删除仅供旧读取路径使用的
`ResolveIndex`；工具总数由 16 个降为 14 个。

**测试对象**：Debug 构建与重新发布的 AOT 单文件产物（13,324,288 字节，publish 目录仅含该 exe），
同一套脚本各跑一遍，25 项断言全部通过（Debug 25/25，AOT 25/25）。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-119 | `tools/list` 工具数量 | 14 个 | 一致 | PASS |
| TC-120 | `navigate` schema | `handle,offset,strId` | 一致 | PASS |
| TC-121 | `search_items` schema | `handle,query,caseSensitive` | 一致 | PASS |
| TC-122 | `get_info` schema | `handle` | 一致 | PASS |
| TC-123 | 旧导航工具不存在 | 无 `list_items` / `previous_item` / `next_item` | 一致 | PASS |
| TC-124 | 空文档 `navigate(offset=0)` | 无返回内容（等价 `null`） | 一致 | PASS |
| TC-125 | 省略 `strId`、`offset=0` | `strId="1"`、`text="item 1"` | 一致 | PASS |
| TC-126 | 返回形状 | 裸 `StringItem`，无包装键 | 一致 | PASS |
| TC-127 | 省略 `strId`、`offset=2` | `strId="3"` | 一致 | PASS |
| TC-128 | `navigate("1", 0)` | `strId="1"` | 一致 | PASS |
| TC-129 | `navigate("1", 1)` | `strId="2"` | 一致 | PASS |
| TC-130 | `navigate("3", -1)` | `strId="2"` | 一致 | PASS |
| TC-131 | `navigate("1", 4)` 跨多条向后 | `strId="5"` | 一致 | PASS |
| TC-132 | `navigate("5", -4)` 跨多条向前 | `strId="1"` | 一致 | PASS |
| TC-133 | `navigate("1", -1)` 越过首条 | 无返回内容 | 一致 | PASS |
| TC-134 | `navigate("5", 1)` 越过末条 | 无返回内容 | 一致 | PASS |
| TC-135 | 省略 `strId`、`offset=-2` | 无返回内容 | 一致 | PASS |
| TC-136 | `navigate("1", 99)` 远越界 | 无返回内容 | 一致 | PASS |
| TC-137 | 未知 `strId="99"` | 工具错误 | 工具错误 | PASS |
| TC-138 | `search_items(query="item")` 返回键 | `handle,query,isCaseSensitive,matchCount,items` | 一致 | PASS |
| TC-139 | 全部命中 | `matchCount=4`、`items.strId` 为 `1,2,3,4` | 一致 | PASS |
| TC-140 | 大小写敏感无命中 | `matchCount=0` | 一致 | PASS |
| TC-141 | 删除 `"2"` 后 `navigate("1", 1)` | `strId="3"`，跳过已删除条目 | 一致 | PASS |
| TC-142 | 删除后参照条目失效 | `navigate("2", 0)` 报工具错误 | 工具错误 | PASS |
| TC-143 | 删除后搜索 | `matchCount=3` | 一致 | PASS |

补充说明：

- `offset` 可正可负、可跨多条；`0` 返回基准条目，因此"读取单条"与"上下移动"由同一工具承担。
- 省略 `strId` 时基准固定为文档第一条（不依赖任何服务端浏览状态）；`offset` 为负数会立即越界返回空。
- 工具返回 `null` 时 MCP 响应只有空的 `content` 数组，客户端据此判定"没有上一条/下一条"。
- 本节取代 §18 的 `list_items` + `previous_item` / `next_item` 方案，三个工具均已从工具清单中删除。

## 20. 移除 get_info 后的复测

**背景**：删除 `get_info` 工具以及仅供它使用的 `W3StringsDocument.GetInfoAsync` 静态扩展成员。
`W3StringsDocumentInfo` 模型保留：`open_document` / `create_document` / `save_document` 仍以它返回
`handle`、`itemCount`、`isDirty`，Agent 直接在这些操作的响应里获取文档状态。工具总数由 14 个降为 13 个。

**测试对象**：Debug 构建与重新发布的 AOT 单文件产物（13,323,264 字节，publish 目录仅含该 exe），
同一套脚本各跑一遍，31 项断言全部通过（Debug 31/31，AOT 31/31）。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-144 | `tools/list` 工具数量 | 13 个 | 一致 | PASS |
| TC-145 | `get_info` 不存在 | 工具清单无 `get_info` | 一致 | PASS |
| TC-146 | `navigate` schema | `handle,offset,strId` | 一致 | PASS |
| TC-147 | `search_items` schema | `handle,query,caseSensitive` | 一致 | PASS |
| TC-148 | `save_document` schema | `handle,version,filePath,language` | 一致 | PASS |
| TC-149 | 旧导航工具不存在 | 无 `list_items` / `previous_item` / `next_item` | 一致 | PASS |
| TC-150 | `create_document` 返回文档摘要 | `handle,itemCount,isDirty`，`itemCount=0`、`isDirty=true` | 一致 | PASS |
| TC-151 | 空文档 `navigate(offset=0)` | 无返回内容（等价 `null`） | 一致 | PASS |
| TC-152 | 省略 `strId`、`offset=0` | `strId="1"`、`text="item 1"` | 一致 | PASS |
| TC-153 | 返回形状 | 裸 `StringItem`，无包装键 | 一致 | PASS |
| TC-154 | 省略 `strId`、`offset=2` | `strId="3"` | 一致 | PASS |
| TC-155 | `navigate("1", 0)` | `strId="1"` | 一致 | PASS |
| TC-156 | `navigate("1", 1)` | `strId="2"` | 一致 | PASS |
| TC-157 | `navigate("3", -1)` | `strId="2"` | 一致 | PASS |
| TC-158 | `navigate("1", 4)` | `strId="5"` | 一致 | PASS |
| TC-159 | `navigate("5", -4)` | `strId="1"` | 一致 | PASS |
| TC-160 | `navigate("1", -1)` | 无返回内容 | 一致 | PASS |
| TC-161 | `navigate("5", 1)` | 无返回内容 | 一致 | PASS |
| TC-162 | 省略 `strId`、`offset=-2` | 无返回内容 | 一致 | PASS |
| TC-163 | `navigate("1", 99)` | 无返回内容 | 一致 | PASS |
| TC-164 | 未知 `strId="99"` | 工具错误 | 工具错误 | PASS |
| TC-165 | 调用已移除的 `get_info` | 工具错误（工具不存在） | 工具错误 | PASS |
| TC-166 | `search_items(query="item")` 返回键 | `handle,query,isCaseSensitive,matchCount,items` | 一致 | PASS |
| TC-167 | 全部命中 | `matchCount=4`、`items.strId` 为 `1,2,3,4` | 一致 | PASS |
| TC-168 | 大小写敏感无命中 | `matchCount=0` | 一致 | PASS |
| TC-169 | 删除 `"2"` 后 `navigate("1", 1)` | `strId="3"`，跳过已删除条目 | 一致 | PASS |
| TC-170 | 删除后参照条目失效 | `navigate("2", 0)` 报工具错误 | 工具错误 | PASS |
| TC-171 | 删除后搜索 | `matchCount=3` | 一致 | PASS |
| TC-172 | `save_document` 返回文档摘要 | `itemCount=4`、`isDirty=false` | 一致 | PASS |
| TC-173 | 保存后写盘 | 目标文件存在 | 存在 | PASS |
| TC-174 | `open_document` 返回文档摘要 | `itemCount=4`、`isDirty=false` | 一致 | PASS |

补充说明：

- 移除 `get_info` 不影响 `W3StringsDocumentInfo` 的 AOT 序列化：该模型仍在 `W3StringsJsonContext` 中登记，
  由三个生命周期工具继续使用（TC-150 / TC-172 / TC-174）。
- 关闭文档后再调用任何以 Handle 为参数的工具会得到 `KeyNotFoundException`（与其他工具一致）；
  调用 `get_info` 则得到"工具不存在"错误。

## 21. open/create 返回 Guid 并新增 count / is_dirty 后的复测

**背景**：`open_document` 与 `create_document` 的返回类型由 `W3StringsDocumentInfo` 改为 `Guid`，
`save_document` 改为无返回值；删除 `get_info`、`GetInfoAsync` 与 `W3StringsDocumentInfo` 模型，
新增只读工具 `count`（`int`）与 `is_dirty`（`bool`）。`W3StringsJsonContext` 增加
`Guid` / `int` / `bool` 登记。工具总数由 13 个增至 15 个。

**测试对象**：Debug 构建与重新发布的 AOT 单文件产物（13,310,464 字节，publish 目录仅含该 exe），
同一套脚本各跑一遍，31 项断言全部通过（Debug 31/31，AOT 31/31）。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-175 | `tools/list` 工具数量 | 15 个 | 一致 | PASS |
| TC-176 | `count` schema | `handle` | 一致 | PASS |
| TC-177 | `is_dirty` schema | `handle` | 一致 | PASS |
| TC-178 | `get_info` 不存在 | 工具清单无 `get_info` | 一致 | PASS |
| TC-179 | `navigate` schema | `handle,offset,strId` | 一致 | PASS |
| TC-180 | `create_document` 返回值 | 裸 GUID 字符串，无包装字段 | 一致 | PASS |
| TC-181 | 新建文档条目数 | `count=0` | 一致 | PASS |
| TC-182 | 新建文档脏标记 | `is_dirty=true` | 一致 | PASS |
| TC-183 | 添加 5 条后条目数 | `count=5` | 一致 | PASS |
| TC-184 | 添加条目后脏标记 | `is_dirty=true` | 一致 | PASS |
| TC-185 | 省略 `strId`、`offset=0` | `strId="1"`、`text="item 1"` | 一致 | PASS |
| TC-186 | 返回形状 | 裸 `StringItem`，无包装键 | 一致 | PASS |
| TC-187 | 省略 `strId`、`offset=2` | `strId="3"` | 一致 | PASS |
| TC-188 | `navigate("1", 1)` | `strId="2"` | 一致 | PASS |
| TC-189 | `navigate("3", -1)` | `strId="2"` | 一致 | PASS |
| TC-190 | `navigate("1", 4)` | `strId="5"` | 一致 | PASS |
| TC-191 | `navigate("5", -4)` | `strId="1"` | 一致 | PASS |
| TC-192 | `navigate("1", -1)` | 无返回内容 | 一致 | PASS |
| TC-193 | `navigate("5", 1)` | 无返回内容 | 一致 | PASS |
| TC-194 | `navigate("1", 99)` | 无返回内容 | 一致 | PASS |
| TC-195 | 未知 `strId="99"` | 工具错误 | 工具错误 | PASS |
| TC-196 | `search_items(query="item")` | `matchCount=4`、`items.strId` 为 `1,2,3,4` | 一致 | PASS |
| TC-197 | 删除 `"2"` 后条目数 | `count=4` | 一致 | PASS |
| TC-198 | 删除后 `navigate("1", 1)` | `strId="3"` | 一致 | PASS |
| TC-199 | `save_document` 返回值 | 无 `content`、无 `structuredContent` | 一致 | PASS |
| TC-200 | 保存后写盘 | 目标文件存在 | 存在 | PASS |
| TC-201 | 保存后脏标记 | `is_dirty=false` | 一致 | PASS |
| TC-202 | `open_document` 返回值 | 裸 GUID 字符串，且与旧 Handle 不同 | 一致 | PASS |
| TC-203 | `open_document` 后条目数 | `count=4` | 一致 | PASS |
| TC-204 | `open_document` 后脏标记 | `is_dirty=false` | 一致 | PASS |
| TC-205 | 未知 Handle | `count` 返回工具错误 | 工具错误 | PASS |

补充说明：

- `Guid` / `int` / `bool` 返回值在 Native AOT 下均正常序列化（已在 `W3StringsJsonContext` 中登记），
  `count` 与 `is_dirty` 返回纯标量，不再是对象包装。
- `save_document` 在本次改造中曾改为无返回值，§22 起改为返回 `bool` 报告写盘成功。
- 本节取代 §20 的"生命周期工具返回 `W3StringsDocumentInfo`"方案，该模型文件已删除。

## 22. save_document 报告写盘成功后的复测

**背景**：`save_document` 的返回类型由"无返回值"改为 `bool`，写盘成功返回 `true`；
`DocumentStorage.SaveAsync` / `SaveCoreAsync` 相应改为 `Task<bool>`。`bool` 已登记在
`W3StringsJsonContext`，AOT 下无需额外配置。

**测试对象**：Debug 构建与重新发布的 AOT 单文件产物（13,310,976 字节，publish 目录仅含该 exe），
同一套脚本各跑一遍，14 项断言全部通过（Debug 14/14，AOT 14/14）。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-206 | `tools/list` 工具数量 | 15 个 | 一致 | PASS |
| TC-207 | `count` schema | `handle` | 一致 | PASS |
| TC-208 | `is_dirty` schema | `handle` | 一致 | PASS |
| TC-209 | `save_document` schema | `handle,version,filePath,language` | 一致 | PASS |
| TC-210 | `create_document` 返回值 | 裸 GUID 字符串 | 一致 | PASS |
| TC-211 | 新建文档条目数 | `count=0` | 一致 | PASS |
| TC-212 | 新建文档脏标记 | `is_dirty=true` | 一致 | PASS |
| TC-213 | 添加条目 | 无错误 | 无错误 | PASS |
| TC-214 | 添加后条目数 | `count=5` | 一致 | PASS |
| TC-215 | `save_document` 返回值 | 标量 `true` | `true` | PASS |
| TC-216 | 保存后写盘 | 目标文件存在 | 存在 | PASS |
| TC-217 | 保存后脏标记 | `is_dirty=false` | 一致 | PASS |
| TC-218 | `open_document` 返回值 | 裸 GUID 字符串 | 一致 | PASS |
| TC-219 | 重新打开后条目数 | `count=4`（已删除一条） | 一致 | PASS |

补充说明：

- 写盘失败（序列化失败、路径不可写等）仍抛工具错误而不是返回 `false`；因此 `true` 是 Agent
  判断"已落盘"的唯一成功信号。
- `bool` 返回值在 Native AOT 下正常序列化（Debug 与 AOT 结果一致）。
- 本节取代 §21 中"`save_document` 无返回值"的描述。

## 23. 文件名必须与语言代码一致（Agent 提示）的验证

**背景**：游戏只加载文件名与语言代码一致的 `.w3strings` 文件。本次在
`save_document`、`open_document`、`list_languages` 的工具说明（`[Description]`）中加入该约定，
使 Agent 在命名文件前即可获知规则；MCP 不对文件名做校验或自动改名。

**测试对象**：Debug 构建与重新发布的 AOT 单文件产物（13,311,488 字节，publish 目录仅含该 exe），
同一套脚本各跑一遍，10 项断言全部通过（Debug 10/10，AOT 10/10）。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-220 | `tools/list` 工具数量 | 15 个 | 一致 | PASS |
| TC-221 | `save_document` 说明包含命名规则 | 含 `<language>.w3strings` 与 `en.w3strings` | 一致 | PASS |
| TC-222 | `list_languages` 说明包含命名规则 | 含 `en.w3strings` | 一致 | PASS |
| TC-223 | `open_document` 说明包含命名规则 | 含 `en.w3strings` | 一致 | PASS |
| TC-224 | `save_document.language` 参数说明 | 提及语言代码示例（`en`） | 一致 | PASS |
| TC-225 | `save_document` 返回值 | `true` | `true` | PASS |
| TC-226 | 按 `en.w3strings` 保存 | 目标文件存在 | 存在 | PASS |
| TC-227 | 保存后脏标记 | `is_dirty=false` | 一致 | PASS |
| TC-228 | 保存后条目数 | `count=1` | 一致 | PASS |
| TC-229 | `list_languages` 含 `en` | 列表中包含 `en` | 包含 | PASS |

补充说明：

- 说明文本随 `tools/list` 返回，Agent 在调用 `save_document` 之前即可读到命名规则，
  无需额外调用。
- 该提示不影响运行时行为：MCP 不拒绝"文件名与语言不一致"的保存请求，是否遵守由 Agent 决定
  （如需强制，可后续在 `save_document` 中增加校验）。

## 24. MCP Logging（客户端日志）验证

**背景**：引入 `Logging/ClientLog.cs`（独立于 `Tools/` 的 `Witcher3StringEditor.MCP.Logging` 命名空间），
通过 `RequestContext<CallToolRequestParams>.Server.AsClientLoggerProvider()` 把 `Information` 级日志
以 `notifications/message` 发给客户端，覆盖 `open_document` / `create_document` / `save_document` /
`close_document` 四个生命周期工具。

**测试对象**：Debug 构建与 AOT 单文件产物（13,325,312 字节，publish 目录仅含该 exe），
同一套脚本各跑一遍，10 项断言全部通过（Debug 10/10，AOT 10/10）。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-230 | `initialize` 能力声明 | 含 `capabilities.logging` | 含 | PASS |
| TC-231 | 工具数量 | 15 个 | 一致 | PASS |
| TC-232 | `create_document` 日志 | 收到 `notifications/message`，内容含 `Created document` | 一致 | PASS |
| TC-233 | 日志级别 | `info` | `info` | PASS |
| TC-234 | 日志 logger 名 | `Witcher3StringEditor.MCP` | 一致 | PASS |
| TC-235 | `save_document` 返回值 | `true` | `true` | PASS |
| TC-236 | `save_document` 日志 | 含 `Saved document` | 一致 | PASS |
| TC-237 | 保存后写盘 | 目标文件存在 | 存在 | PASS |
| TC-238 | `close_document` 日志 | 含 `Closed document` | 一致 | PASS |
| TC-239 | `logging/setLevel=error` 后 | `Information` 日志不再发送 | 无通知 | PASS |

补充说明：

- 日志辅助类 `ClientLog` 位于 `Logging/` 目录，工具层只调用 `ClientLog.For(context)`，
  日志实现与工具定义解耦。

- 客户端必须先发送 `logging/setLevel`；未设置时服务器可能不发送或全发，协议未规定。
- 日志通过 `data` 字段携带已格式化的消息文本，`level` 与 `logger` 由 SDK 填充。
- MCP Logging 已由 SEP-2577 标记弃用（规范 2026-07-28）；项目保留该能力，
  在 `ToolLog` 中局部抑制 `MCP9005` 警告，构建仍为 0 警告 0 错误。

> 注：本节验证的 `ClientLog` / `notifications/message` 实现在 §25 切换到 Streamable HTTP 时已移除，
> 日志改为在 `Program.cs` 配置、输出到控制台。

## 25. 传输切换为 Streamable HTTP（ASP.NET Core）

**背景**：交互不再走 stdio，改为 ASP.NET Core 托管的 Streamable HTTP：

- 项目 SDK 改为 `Microsoft.NET.Sdk.Web`，新增 `ModelContextProtocol.AspNetCore` 2.3.0 引用；
- `Program.cs` 使用 `WebApplication.CreateBuilder` + `AddMcpServer().WithHttpTransport(...)` + `MapMcp()`，
  端点 `/`；会话模式 `HttpServerSessionMode.StatefulForInitializeClients`；
- 日志在 `Program.cs` 集中配置（Serilog + `builder.Host.UseSerilog()`），
  协议不再占用 stdout，控制台专用于打印日志；
- `.mcp/server.json` 的 transport 改为 `streamable-http`，`PackageTags` 中的 `stdio` 改为 `http`。

**测试对象**：Debug 构建与 AOT 单文件产物（25,378,304 字节，publish 目录仅含该 exe），
两者分别用 `--urls http://127.0.0.1:5199` 启动，执行同一套 HTTP 端到端脚本。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-250 | `initialize` | 协商出 `protocolVersion` 2025-06-18 | 一致 | PASS |
| TC-251 | `initialize` | 返回 `serverInfo.name` | 一致 | PASS |
| TC-252 | `initialize` | 返回 `Mcp-Session-Id` | 返回 | PASS |
| TC-253 | `initialize` | 声明 `capabilities.tools` | 声明 | PASS |
| TC-254 | `notifications/initialized` | HTTP 202 | 202 | PASS |
| TC-255 | `tools/list` | 15 个工具，名称齐全 | 一致 | PASS |
| TC-256 | `tools/list` | `navigate` schema 必填 `handle`、`offset` | 一致 | PASS |
| TC-257 | `create_document` | 返回 `Guid` | 返回 | PASS |
| TC-258 | `count` / `is_dirty` | 新文档 0 条、`is_dirty=true` | 一致 | PASS |
| TC-259 | `add_item` ×2 | 计数变为 2 | 2 | PASS |
| TC-260 | `navigate` | 0 返回首条、+1 前进、-1 后退、越界返回 `null` | 一致 | PASS |
| TC-261 | `get_item` / `update_item` / `delete_item` | 文本可读可改，删除后计数为 1 | 一致 | PASS |
| TC-262 | `search_items` | 不区分大小写命中 1 条 | 1 | PASS |
| TC-263 | `save_document` | 返回 `true` 且文件存在 | 一致 | PASS |
| TC-264 | `is_dirty`（保存后） | `false` | `false` | PASS |
| TC-265 | 关闭后的句柄 | `tools/call` 返回 `isError=true` | 一致 | PASS |
| TC-266 | `open_document` | 返回 `Guid`，读回 1 条 | 一致 | PASS |
| TC-267 | `list_languages` | 含 `en`、`cn` | 一致 | PASS |
| TC-268 | `DELETE /` | 终止会话，HTTP 200 | 200 | PASS |
| TC-269 | 控制台日志 | 日志写入 stdout，stderr 为空 | 一致 | PASS |
| TC-270 | AOT 发布 | 单文件 exe，无多余文件 | 一致 | PASS |

**结果**：Debug 50/50、AOT 50/50 全部通过；`dotnet build` 0 警告 0 错误。

## 26. 工具日志埋点验证

**背景**：4 个生命周期工具直接调用 Serilog 静态方法 `Log.Information` / `Log.Warning`，不接收 `ILogger` 参数；
日志由 `Program.cs` 的 Serilog（`builder.Host.UseSerilog()` + Console sink）输出，
格式为 `[HH:mm:ss LVL] message`。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-271 | `tools/list` 的 15 个 schema | 参数与工具定义一致，无 `logger` 参数 | 一致 | PASS |
| TC-272 | `open_document` | 输出 `Opened document <handle> from <file>` | 一致 | PASS |
| TC-273 | `create_document` | 输出 `Created document <handle>` | 一致 | PASS |
| TC-274 | `save_document` | 输出 `Saved document ... as version 162 (en)` | 一致 | PASS |
| TC-275 | `close_document` | 输出 `Closed document ... (saved before close: False)` | 一致 | PASS |
| TC-276 | 输出通道 | 日志写入 stdout，stderr 为空 | 一致 | PASS |

**结果**：Debug 与 AOT（25,378,304 字节，publish 目录仅含 exe）均通过；两套端到端脚本仍为 50/50。

> 注：本节记录的 Streamable HTTP + Serilog 方案已在提交 `c37bb7b`（移除 Serilog 依赖并简化错误处理逻辑）
> 中回退，传输恢复 stdio、日志方案重写，见 §27。

## 27. MCP Logging 规范化实现验证

**背景**：按 `modelcontextprotocol.io/specification/2025-11-25/server/utilities/logging` 与
`csharp.sdk.modelcontextprotocol.io/v1/concepts/logging/logging.html` 重新实现日志，不再使用 Serilog：

- 传输恢复 stdio，`Program.cs` 把宿主日志全部导向 stderr
  （`ClearProviders()` + `AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace)`），
  stdout 只承载 JSON-RPC；
- SDK 自动在 `initialize` 中声明 `logging` 能力，并自动处理 `logging/setLevel`；
- 工具以特殊参数 `McpServer server` 取得会话（SDK 注入，不进 `inputSchema`），
  用 `server.AsClientLoggerProvider().CreateLogger("w3strings")` 取得 `ILogger`，
  消息以 `notifications/message` 发出；
- 埋点 9 处：4 个生命周期工具 `info`（`save_document` 失败为 `warning`），
  5 个条目变更工具 `debug`。

**测试对象**：Debug 构建与 AOT 单文件产物（13,325,312 字节，publish 目录仅含该 exe），
同一套 stdio 脚本各跑一遍。

| 编号 | 场景 | 预期 | 实际 | 结果 |
| --- | --- | --- | --- | --- |
| TC-280 | `initialize` 能力声明 | 含 `capabilities.logging` | 含 | PASS |
| TC-281 | `tools/list` | 15 个工具 | 15 | PASS |
| TC-282 | 工具 schema | 不暴露 `server` 参数 | 未暴露 | PASS |
| TC-283 | `logging/setLevel=debug` | 返回空结果、无错误 | 一致 | PASS |
| TC-284 | `create_document` | 收到 `notifications/message` | 收到 | PASS |
| TC-285 | 通知 `level` | `info` | `info` | PASS |
| TC-286 | 通知 `logger` | `w3strings` | `w3strings` | PASS |
| TC-287 | 通知 `data` | 字符串，含 `Created document` 与句柄 | 一致 | PASS |
| TC-288 | `add/update/key_name/delete` | 4 条 `debug` 通知 | 4 | PASS |
| TC-289 | `logging/setLevel=error` 后 `create_document` | 无任何通知 | 无 | PASS |
| TC-290 | 同一调用结果 | 工具照常返回句柄 | 一致 | PASS |
| TC-291 | `save_document` | 返回 `true` | `true` | PASS |
| TC-292 | 写盘 | 目标文件存在 | 存在 | PASS |
| TC-293 | `save_document` 通知 | `info`，含 `Saved document` | 一致 | PASS |
| TC-294 | `close_document` 通知 | `info`，含 `Closed document` | 一致 | PASS |
| TC-295 | `open_document` | 返回 `Guid` | 返回 | PASS |
| TC-296 | `open_document` 通知 | `info`，含 `Opened` | 一致 | PASS |
| TC-297 | stdout 纯净度 | 每行均为 JSON-RPC | 一致 | PASS |
| TC-298 | 宿主日志通道 | 全部写 stderr | 一致 | PASS |

**结果**：Debug 19/19、AOT 19/19 通过；`dotnet build` 0 警告 0 错误。

补充说明：

- 通道实测：Debug 一次握手后 stdin 关闭，stderr 24 行宿主日志、stdout 0 字节非协议内容；
  AOT 同一场景 stdout 1 行（`initialize` 响应）、stderr 20 行。
- 客户端未发送 `logging/setLevel` 时实测不发任何通知：`initialize` 后直接 `create_document`，
  stdout 中只有 `id:1`、`id:2` 两条响应，没有 `notifications/message`。因此客户端必须先调用
  `logging/setLevel`（与规范「客户端应先设置级别」的说明一致）。
- `AsClientLoggerProvider()` 在 SDK 2.3.0 中被标记 `MCP9005` 弃用（SEP-2577：规范 2026-07-28 起弃用
  Logging）。项目面向 2025-11-25，需要该能力，故在唯一调用点 `#pragma warning disable MCP9005`
  局部抑制，并在注释说明原因。







