# 模块 B：素材归置、产物审计与公开前清理方案

> 状态：模块 B 已完成。步骤 1 文档、步骤 2 原素材副本、步骤 3 构建路径与发布边界、已确认的 worktree/旧副本清理、验证证据归档均已执行并验证。候选素材仍按约定保留，未用素材不自动删除。
> 日期：2026-09-14

## 1. 已核验结论

| 项目 | 结论 |
|---|---|
| `assets/source/` | 3,015 个文件，其中 2,961 个 PNG，约 1.43 GiB；来自 `候选素材_官方/` 的原样副本。 |
| 内容一致性 | 2,961 个 PNG 逐文件 SHA-256 一致，缺失、长度不一致、哈希不一致均为 0。 |
| 当前构建输入 | `config/animation-sources.json`、`config/scene-rules.example.json` 和 9 个素材工具已切换读取 `原素材/`。 |
| 历史清单 | `assets/manifests/animations.json` 中的旧 `sourcePath` 仅作来源记录，不参与运行时加载。 |
| 发布包 | 当前打包脚本不复制 `assets/source/`、候选素材或 `.local-tools/`。迁移到根目录后包体积预计不变。 |
| 包体积 | 现有 MSIX 约 195–200 MiB；旧便携包约 170 MiB。主要体积来自运行时资源和运行库。 |
| `.local-tools/` | 289 个 PNG、约 134.5 MiB；正常编译不依赖，但 `ai-bun-source-frames` 具有源帧保留价值。 |
| `docs/validation/` | 182 个 PNG、约 26.2 MiB；其中 178 个已跟踪，PNG 使用 Git LFS。建议只保留精选验收证据。 |

精选验收证据清单已写入 `docs/项目约定.md`；其余历史截图目前只标记为可归档，不在本轮删除或移动。

## 2. `artifacts/` worktree 判断

`git worktree list` 显示 `artifacts/` 下实际登记 9 个 worktree，不是 11 个。另有 4 个历史 worktree 位于 `artifacts/` 外。

| 路径 | 分支/提交 | 状态 | 判断 |
|---|---|---|---|
| `artifacts/afternoon-greeting-worktree` | `codex/afternoon-hurry-greeting` | 干净，已合并 | 可在确认后移除 |
| `artifacts/compact-tray-worktree` | `codex/compact-tray-menu` | 干净，有 3 个独有提交 | 保留 |
| `artifacts/music-fix-worktree` | detached `7dcf468f` | 2 个未提交文件 | 保留 |
| `artifacts/music-settings-feedback-worktree` | `codex/update-animation-picker-20260912` | 干净，有 1 个独有提交 | 保留 |
| `artifacts/notification-settings-worktree` | `codex/notification-settings-redesign` | 干净，已合并 | 可在确认后移除 |
| `artifacts/notification-worktree` | `codex/notification-side-details` | 干净，已合并 | 可在确认后移除 |
| `artifacts/preserve-drag-worktree` | `codex/preserve-animation-drag` | 干净，已合并 | 可在确认后移除 |
| `artifacts/top-drag-fix-worktree` | `codex/fit-tray-menu-content` | 干净，已合并 | 可在确认后移除 |
| `artifacts/tray-fix-worktree` | detached `292cfc4e` | 17 个未提交文件 | 保留 |

另外 4 个登记 worktree：

- `.codex/worktrees/luo-planner-20260913`：已合并且干净；
- `Documents/Codex/.../quick-actions-verified`：23 个未提交文件；
- `Documents/Codex/.../stable-layout`：已合并且干净；
- `Documents/Codex/.../feedback-commit`：有 1 个独有提交。

本轮不删除任何 worktree。

## 3. 修订后的 Module B 目标

```text
项目根目录/
├── assets/                 # 运行时资源和正式加工产物，进 Git 和发布包
├── 原素材/                 # 原始素材和构建输入，不进 Git，不进发布包
│   ├── animations/
│   ├── models/
│   └── ui/
├── 候选素材_官方/          # 未使用候选素材，保留给用户手动清理
├── config/
├── tools/
├── docs/
└── tests/
```

实施时只复制，不移动候选素材。迁移顺序：

1. 将实际使用的 `assets/source/` 内容复制到 `原素材/`。已完成。
2. 将需要重建的 `.local-tools/work/ai-bun-source-frames/` 等源帧复制到 `原素材/`。
3. 更新配置、工具和预览输出路径。已完成。
4. 在打包脚本中显式排除 `原素材/`、候选素材和 `.local-tools/`。已完成规则和静态验证。
5. 编译、测试、重建代表性动画并检查包内容。
6. 只有验证完成并得到确认后，才处理 `assets/source/` 旧副本。

中文目录名 `原素材` 在 Windows、.NET、MSBuild、Git 和 Git LFS 中可用。脚本必须使用 `-LiteralPath`、UTF-8 和路径 API。若未来需要跨平台或兼容旧第三方工具，可改用 `raw-assets/`。

## 4. 后续新素材规则

1. 先记录来源目录和素材用途。
2. 搜索动画清单、状态映射和构建脚本，判断是否实际使用。
3. 只复制实际使用或需要重建的素材到 `原素材/`。
4. 按 `animations/`、`models/`、`ui/` 分类，尽量保留原始文件名。
5. 同名文件先比较哈希，禁止未经确认覆盖。
6. 未使用素材留在原目录，不迁移、不删除。
7. 更新 `docs/原素材索引.md`，记录来源、目标、用途和 SHA-256。
8. 每次输出新增文件、目标路径、来源、数量、哈希和是否参与构建。

## 5. 项目约定文档方案

建议建立并互相引用：

| 文件 | 内容 |
|---|---|
| `AGENTS.md` | 本地维护入口；目录、素材、Git、发布和变更硬约束。 |
| `docs/项目约定.md` | 面向开发者的详细目录、数据、Git、发布、命名和变更规则。 |
| `docs/原素材索引.md` | 原素材来源、用途、目标路径和哈希。 |
| `.gitignore` | 排除 `原素材/`、候选素材、`artifacts/`、`.local-tools/`、bin/obj、日志和缓存。 |
| `.gitattributes` | PNG、WebP、GIF、音视频和模型使用 Git LFS；代码、配置和文档不使用 LFS。 |
| `README.md` | 顶部链接 `docs/项目约定.md`，不链接本地维护入口。 |
| `docs/项目总控.md` | 记录当前结构、文档位置、风险和唯一下一步。 |

正式资源只放 `assets/`；原素材只放 `原素材/`；构建产物只放 `artifacts/`；中间帧和本地工具只放 `.local-tools/` 或 `%TEMP%`。

## 6. AI 痕迹清理方案

已发现的主要位置：

| 位置 | 处理建议 |
|---|---|
| `AGENTS.md`、`README.md`、`docs/项目总控.md` | 删除工具名称和“由工具维护”等表述，改为中性项目约定。 |
| `docs/精细模型动画任务开场提示词.md` 等 | 将工具专用任务措辞改为普通维护任务。 |
| `docs/design/`、`docs/开发日志.md`、`docs/动画状态映射.md` | 将“AI Agent”“AI 视频”等改为中性描述。 |
| `.gitignore` 注释 | 改为“视频帧导出”“互动帧导出”。 |
| `process_bun_video.py`、`bun-eat*.meta.json` | 已完成文件名中性化；运行时动画 ID 保持不变。 |
| Git 历史 | 发现 2 条合并提交信息包含 `codex/` 分支名；不建议现在重写历史。正式公开前另做备份和历史清理。 |
| `.codex/`、`.cursor/`、`.claude/` | 仓库根目录未发现；用户级工具目录不复制进仓库。 |

推荐方案：Git 中保留中性的 `DEVELOPMENT.md` 和 `docs/项目约定.md`；本地保留被 `.gitignore` 排除的 `AGENTS.md`，供本地维护工具读取。

## 7. 后续执行顺序

1. 建立项目约定文档和索引。
2. 复制构建输入到 `原素材/`。
3. 修改构建配置、工具路径和打包排除规则。
4. 编译、测试、重建代表性动画并检查发布包。
5. 经确认后处理 `assets/source/`、`.local-tools/` 和已合并 worktree。
6. 最后单独清理公开文案、文件名和必要的 Git 历史。

步骤 2、步骤 3 已执行并完成验证；5 个已合并 worktree 和旧 `assets/source/` 已按确认清理。候选素材、`.local-tools/` 和 `docs/validation/` 归档对象仍未处理。
