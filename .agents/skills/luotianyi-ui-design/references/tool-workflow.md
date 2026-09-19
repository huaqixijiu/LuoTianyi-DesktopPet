# 可复现工具流程

以下命令从仓库根执行；仅 npm 安装需切到 `tools/ui-review`。所有截图和新目录用本轮唯一标识，不能复用真实用户存档。

## 安装与编译

```powershell
dotnet build LuoTianyiPet.sln -c Release --no-restore --nologo
dotnet build tools/ui-review/UiReviewHost/UiReviewHost.csproj -c Release --nologo
dotnet build tools/ui-review/WindowCapture/WindowCapture.csproj -c Release --nologo
Push-Location tools/ui-review
npm ci --ignore-scripts
npm test
Pop-Location
```

依赖固定为 pixelmatch 7.2.0 / pngjs 7.0.0，lockfile 带 integrity；仅开发目录安装。`.npmrc` 指向官方 registry、项目内缓存，禁用生命周期脚本。已安装后图片比较离线运行，无 API Key/OAuth/付费服务；Codex 本身账号要求不变。不要运行来源不明的一键安装器。

## 真实窗口

```powershell
$evidence = Join-Path (Get-Location) ('artifacts/ui-review/session-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
Start-Process -FilePath tools/ui-review/UiReviewHost/bin/Release/net48/UiReviewHost.exe -ArgumentList ('"' + $evidence + '"') -WindowStyle Hidden
```

宿主显示的是正式程序集的真实 PlannerWindow，使用真实 ReminderService，测试数据保存在新证据目录。宿主绕开完整桌宠启动和外部集成，不验证托盘/桌宠附件/通知/音乐链路。日期来自系统时间，不能把不同日期两次运行当稳定基线；跨日时调整隔离 fixture 或明确排除日期差异。

首次显示输出 `render-001.png`、元数据和 `ready.txt`；F8 捕获当前 WPF surface。失败输出 `FAILED.txt`。宿主要求不存在的新目录；正常关闭窗口释放 service。其他页面需要相应 QA，不假定宿主覆盖全部产品。

先按现有 computer-use Skill 初始化 `node_repl` 的 `@oai/sky`，用 `list_windows()` 精确选中 `UiReviewHost` 的唯一返回窗口，再读取 `get_window_state({window, include_screenshot:false, include_text:true})`。只读测试窗口；不要采集其它应用内容。重新观察后才能执行下一次操作。

本机 sky 原生截图和 click 存在接口兼容问题；UIA 读取和 activate_window 可用。不要无限重试或关闭安全功能。以下回退使用源码构建的受限本地工具：

```powershell
# 句柄必须来自刚才实际返回的窗口，AutomationId 来自当前 UIA 树。
& tools/ui-review/WindowCapture/bin/Release/net48/WindowCapture.exe <handle> --invoke ViewWeek
# 再读 UIA 确认周视图，然后激活目标窗。截图前确保窗口无遮挡、完整在屏幕内。
& tools/ui-review/WindowCapture/bin/Release/net48/WindowCapture.exe <handle> artifacts/ui-review/<session>/desktop-week.png
```

回退限制进程名为 UiReviewHost；未知/隐藏/多匹配/禁用/不支持 InvokePattern 的控件拒绝操作。截图只裁目标前台窗口，不抓全屏；非前台、最小化或离屏拒绝。置顶覆盖、鼠标特效仍可能入镜，必须查看截图检查。不要把这个工具当全局输入工具。需要验证拖动/物理输入而 sky 不可用时，明确记录该项阻塞，不用 RaiseEvent/InvokePattern 冒充。

PowerShell 5.1 在本机禁止脚本执行；工作流不更改 ExecutionPolicy，也不使用 bypass 参数。本地工具是可审查的 C# 源码，由现有 SDK 构建。

## 对比与门禁

```powershell
node tools/ui-review/compare.mjs reference.png actual.png artifacts/ui-review/<session>/comparison 0
```

输出 `side-by-side.png`、`overlay.png`、`diff.png`、`result.json`。退出码 0=在容差内，1=视觉像素变化超限，2=输入错误（含尺寸不一致）。默认像素敏感度 0.1、忽略抗锯齿，允许变化比例默认 0；不是“100% 完全像素一致”的声明。须查看全部视觉证据，尤其局部重要区域。最后一个参数只能表示经说明的回归容差，不是审美评分。

同图自比必须零变化；对两张不同状态真实截图运行应发现变化。本轮验证使用月/周两态，只证明比较能力，不证明参考图设计还原已经完成。新参考图到来后必须按该图重新建立设计合同。不同尺寸直接失败；如确需裁切，保留原图、记录裁切区域、输出独立派生文件。不要自动接受当前截图为基线。

`npm test` 覆盖同图、变更、输出尺寸及非法输入。业务改动另运行相关 .NET 测试；本轮全量命令是 `dotnet test LuoTianyiPet.sln -c Release --no-build --no-restore`。

## 基线升级与回滚

Skill 安装后运行 `node tools/ui-review/verify-skill-discovery.mjs <codex.exe绝对路径>`，通过本机只读 app-server skills/list 确认 repo skill enabled=true，不创建任务、不启动模型；结果写入 `artifacts/ui-review/skill-discovery.json`。格式校验使用 skill-creator 的 quick_validate.py；本机验证用 PyYAML 6.0.3 临时安装于 `.local-tools/ui-skill-validation`，不改变全局 Python。

只有用户明确接受的视觉版本才能成为“已批准基线”；保留原基线与证据说明。依赖升级单独修改版本、重装、重跑比较测试和真实截图。回滚本工具链不影响业务程序集源文件；删除工具/Skill 需保留用户自己的证据和参考图。未安装额外 MCP server，也无常驻端口。
