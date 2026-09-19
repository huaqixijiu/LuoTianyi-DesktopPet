# UI 开发能力工具链：调研与验收

日期：2026-09-19。范围：建立能力，不重做 UI，不改业务逻辑、存档、班休、日历或提醒规则。

## 1. 代码核验的实际技术栈

| 项目 | 实际证据与结论 |
|---|---|
| 语言/桌面/UI | C# + WPF；App.csproj 的 UseWPF=true、TargetFramework=net48；不是当前运行在 .NET 10 的应用 |
| 构建 | global.json 固定 .NET SDK 10.0.400，MSBuild/SDK-style csproj；本机 SDK 匹配 |
| 前端框架/CSS | 没有 React/Vue/Electron/WebView 页面；Planner 使用 C# 创建 WPF 控件树 |
| 样式 | PlannerTheme.cs 的语义 Brush、XamlReader 载入的 ResourceDictionary、Style/ControlTemplate；尺寸/布局也散布于 PlannerWindow 各 partial 文件 |
| 组件 | 原生 WPF + 项目自定义 Planner 组件；SkiaSharp 3.119.4 用于图像能力，不是 UI 组件库 |
| 运行 | tools/Launch-LuoTianyiPet.ps1 构建后启动 Release/Debug net48 exe；Bootstrap/StartupOptions 支持 portable、QA 和预览参数；另有 MSIX 发布路线 |
| 项目 Agent 配置 | 本轮前根 AGENTS.md（本地忽略）存在；项目根未发现 .agents Skill、.codex MCP/Agent 配置；artifacts 中旧 worktree 的文件不算当前项目配置 |
| 会话工具 | 已有 Codex computer-use、浏览器 CUA、图像查看能力；浏览器能力不适用于原生 WPF。新增工具没有修改用户级配置 |
| 既有自动化 | Diagnostics/Qa/MainWindow.PlannerQa.cs：隔离 ReminderService、RaiseEvent、反射、布局检查。不是外部真实鼠标测试 |
| 既有截图 | RenderTargetBitmap 自动截图及 100/125/150% 渲染输出；不等于真实桌面 DPI 切换 |
| 既有视觉比较 | 本轮前未发现 Planner 的像素差异门禁、参考图叠图工具或统一设计评审 Skill；素材处理 QA 是另一用途 |

早期 AGENTS 的 .NET 10 LTS 是计划背景；当前技术架构/路线图和 csproj 已转为 net48 发布。本轮不迁移框架。

## 2. 真正缺口

已有功能/布局测试不是零基础。当前短板是参考图缺少可检验的设计合同、实际窗口截图与渲染快照容易混淆、缺少持久的视觉差异与评审记录、通过布局断言后仍可能提前结束。

本轮真实截图观察：月历结构完整，无明显裁切，但大面积浅蓝网格和同族蓝色文字的权重接近；周历空态高度一致而信息密度较低；编辑器边框、分隔线和较大组间空隙共同强化表单感。这些是后续可评审方向，不是本轮擅自更改设计的理由。UIA 中部分带可视文字的按钮自身未暴露清晰 Name，后续应核对无障碍名称和焦点。

## 3. 搜索、来源、维护和取舍

直接阅读官方文档/上游 GitHub，使用 GitHub API 核实维护时间；不是按搜索排名安装。pushed_at 表示仓库活动，不代表每项功能刚更新。

| 候选/来源 | 解决的问题、适配与维护证据 | 决策及重复成本 |
|---|---|---|
| [Microsoft Windows 设计文档](https://learn.microsoft.com/en-us/windows/apps/design/basics/content-basics)、[可访问文本](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessible-text-requirements) | 桌面布局、间距、文字层级和可读性；布局页更新 2026-09-11 | 采用原则，写入一个 WPF 项目 Skill；不直接移植 WinUI API |
| [OpenAI Skills](https://developers.openai.com/zh-Hans/docs/build-skills) + 本机 skill-creator | Codex 项目级发现、说明及执行流程；实际本机 skills/list 可验证 | 采用 .agents/skills/luotianyi-ui-design，启用隐式调用 |
| [Anthropic frontend-design](https://github.com/anthropics/skills/tree/main/skills/frontend-design) | 视觉方向、自我评审；仓库 2026-09-10 活动；包含网页/CSS建议 | 不安装；本项目中文桌面参考图还原比网页视觉冒险优先，避免重叠指令；仅参考思路，未复制上游文件 |
| [UI UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill) | 样式/排版/颜色检索，当前确实包含 WPF；仓库 2026-09-19 活动 | 不安装整套；不是“不支持 WPF”，而是此轮无需广泛风格推荐库、全局 CLI 和重复规则；其参考图闭环仍需另建 |
| 现有 computer-use / @oai/sky | 原生 Windows UIA、激活、截图/输入；当前安装版 26.915.31945 | 复用；已验证读取和激活。本机截图接口及 click 失败，不能声称全部可用 |
| [FlaUI](https://github.com/FlaUI/FlaUI) | 成熟 .NET UIA 包装、WPF 自动化；仓库 2026-08-13 活动，最新正式 release v5.0.0/2025-02-25 | 暂不安装；当前基础 InvokePattern 可由系统 UIA 完成，额外测试框架重复。复杂长期自动化可重新评估 |
| [Playwright MCP](https://github.com/microsoft/playwright-mcp) | 浏览器 DOM/可访问树、自动操作和截图；v0.0.82 发布 2026-09-18 | 不采用；真实产品没有浏览器 DOM，安装浏览器不能验收 WPF |
| [Pixelmatch](https://github.com/mapbox/pixelmatch) + [pngjs](https://github.com/pngjs/pngjs) | 独立于 UI 框架的 PNG 比较、抗锯齿处理；Pixelmatch 仓库 2026-09-15 活动；npm 当前 7.2.0/7.0.0 | 采用，精确锁版本；仅两个开发包，无业务运行依赖 |
| [Figma 官方 MCP](https://developers.figma.com/docs/figma-mcp-server/local-server-installation/) | 设计节点、变量及设计上下文，适合有 Figma 源稿时 | 不安装/不登录；本轮没有 Figma 源稿，图片参考不需要账号；不会自动产出可直接落地的本项目 WPF |
| [Accessibility Insights for Windows](https://github.com/microsoft/accessibility-insights-windows) | 原生无障碍检查；微软开源，仓库 2026-09-15 活动 | 暂不装桌面工具；当前先用已具备的 UIA 树、视觉和焦点检查流程；后续完整无障碍专项有价值，不声称已有完整自动审计 |

## 4. 每项候选的安装/安全/调用成本

| 候选 | 网络、账号、Key、费用 | 依赖/全局影响/风险 | Agent 调用与验证方法 |
|---|---|---|---|
| Microsoft 指南 | 读取文档联网；无需账号/Key/付费 | 纯文档；无安装 | 读取规则后针对真实截图出评审表；本轮已做 |
| 项目 Skill | 本地调用；不新增账号/Key/收费 | 纯项目文件；指令冲突须服从用户和业务规则 | 格式校验、skills/list、实际读取执行；全部通过 |
| Anthropic Skill | 下载联网，本地文本无 Key；本轮不涉及额外付费 | 少量文本；上游 license 需按文件处理；网页规则偏移风险 | 可由 Codex 读文件调用；未安装，故不称运行验证 |
| UI UX Pro Max | 开源基础版本地 Python 搜索无需联网/Key；Premium 非本轮范围 | Python/数据集/可选 npm CLI；推荐全局安装但可选本地；建议偏离品牌风险 | 可运行 search.py 验证 WPF 查询；本轮未安装，未执行 |
| computer-use | 复用已配置能力，不新增认证/费用 | 已有原生 helper；可操作桌面，严格限定测试目标 | 实测 UIA/激活成功，截图/click 失败，使用回退 |
| FlaUI | NuGet 还原联网，运行本地，无 Key/账号/服务费 | 新增 .NET 测试包；无需全局安装；UI 操作应隔离数据 | 可编译测试宿主调用；本轮未安装 |
| Playwright MCP | 下载 npm/浏览器联网；本地浏览器运行无需 Key，目标网站另论 | Node+浏览器体积大；MCP 配置；网页内容和账号访问风险 | 可通过 MCP 调用，但不能操作本项目 WPF，未安装 |
| Pixelmatch/pngjs | 首次 npm 联网，比较离线；无 Key/OAuth/费用 | 2 个包、项目 node_modules；禁用安装脚本，锁定 integrity；PNG 输入尺寸校验 | node compare.mjs；同图/异图/尺寸/输入保护测试通过 |
| Figma MCP | 需 Figma 访问权限/账号；远程路径涉及授权，计划限额应现场核实 | 桌面应用或远程连接；可能传输设计信息 | 有源稿后调用设计上下文/截图工具；本轮不授权、不安装 |
| Accessibility Insights | 下载联网，常规本地分析无需 API Key/账号/服务费 | 桌面安装/更新及遥测选项需审查；不是 npm 项目依赖 | 以检查器验证原生控件及键盘可达性；本轮未安装 |

没有修改全局 Node/Python/.NET、安全设置、防火墙或用户认证。npm 原全局缓存目录不可写，改为项目内缓存；未提权。为运行 skill-creator 官方校验器，在被忽略 `.local-tools/ui-skill-validation` 安装 PyYAML 6.0.3 官方 PyPI wheel，仅验证辅助，不是项目运行依赖。

## 5. 最终保留的工具

1. **一个项目 Skill**：设计准则、参考图拆解、WPF 实现策略、视觉评审、业务保护与闭环门禁。
2. **UiReviewHost（源码）**：运行正式程序集的真实 PlannerWindow/ReminderService，独立 UserData，新建样例，F8 输出渲染快照；无正式业务改动。
3. **WindowCapture（源码）**：本机兼容回退。仅限 UiReviewHost，UIA 精确 ID 调用和前台窗口裁图。拒绝未知、隐藏、禁用、离屏等目标；不承诺遮挡捕获或物理鼠标能力。
4. **Pixelmatch 7.2.0 + pngjs 7.0.0**：合为一个比较工具，生成 diff/overlay/side-by-side/JSON，配退出码与防覆盖检查。
5. **复用既有 computer-use 和 QA**：UIA 观察/定位/激活、已有批量行为/布局/渲染验证；无新增 MCP server、端口或常驻服务。

新开发宿主按类型名反射访问内部 PlannerWindow；若以后改名/改构造器需同步更新工具，失败时不降级为空白假窗口。

## 6. 已实际完成的验证

- 正式解决方案 Release/net48 编译 0 警告、0 错误；两项开发工具单独编译均通过。
- 业务测试 **887/887**：Core 703、Animation 37、Platform.Windows 147。
- 完整正式应用复制到独立测试目录后，以 `--portable --qa-planner` 实际启动并完成，294 条 PASS、100 张 PNG，无 FAILED.txt；不接触原用户存档。该检查仍是既有内部 QA，外部控件操作另由宿主验证。
- npm 安装只有两个包，安装时审计 0 已知漏洞（不是永久安全保证）。比较单测含同图、变更块、尺寸不匹配、非法阈值、输出防覆盖。
- Skill 官方校验器 `Skill is valid!`；本机 Codex app-server `skills/list` 返回 repo、enabled=true 和准确项目路径。没有创建额外任务或发起模型运行。证据在 `artifacts/ui-review/skill-discovery.json`。
- 真正显示月历；UIA 操作到周历、闹钟、创建提醒弹层，逐次读取新控件树确认；生成四张 1200×810 真实桌面裁图。已亲自查看月/周/弹层及差异图，空闹钟页与 UIA 状态一致。
- 同张真实月历图比较：0 像素变化、退出 0；月历与周历比较：30,600 个变化像素（3.148148%）、退出 1，证明差异确实会阻止回归门禁。该数字不是参考图还原分数。
- 真窗口截图与初始 WPF 渲染快照都已生成；句柄 0 被拒绝，没有生成截图。调用失效/未知控件与非测试进程必须失败关闭。

证据目录：`artifacts/ui-review/session-20260919/`。截图和合成图仅本地，不推送私人参考图或真实用户数据。

明确边界：本轮没有新设计参考图，因此验证了“两图比较能力”，没有虚报某份设计已还原；没有测试物理拖拽、屏幕缩放切换、多显示器、读屏器和高对比模式。UIA 调用不是鼠标点击验收。首张桌面图可见鼠标特效，已在评审中标注，不能直接成为稳定批准基线。

## 7. 后续工作流与人工事项

参考图 → 读项目规则 → 分析设计语言并写合同 → 看当前实窗 → 局部实现 → 编译/测试 → 运行隔离真实窗口 → 操作目标状态 → 截图并实际查看 → 原图/叠图/差异图逐项审查 → 修正 → 重新运行截图 → 记录验收及剩余差异。

必须同时满足功能和视觉验收。不得自动用新截图覆盖批准基线，不得把 PNG 比例当美观评分，不得一次改样式后直接交付。

**无需用户手动操作。** 本轮工具链不要求 OAuth、API Key 或第三方账号；Figma 属于未选方案。以后用户发出参考图设计任务时，项目规则会要求读取这个 Skill。本轮没有重做页面。
