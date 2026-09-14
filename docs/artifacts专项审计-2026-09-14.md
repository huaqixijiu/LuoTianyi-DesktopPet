# artifacts 专项审计（2026-09-14）

## 审计边界

- 本次只读检查 `artifacts/` 顶层目录、Git worktree、分支提交关系和工作区状态。
- 未执行 `git worktree remove`、分支删除、文件删除或文件移动。
- 当前统计：16,902 个文件，约 18.06 GiB。

## 顶层清单

| 路径 | 类型 | 文件数 | 大小 MiB | 分类与判断 |
|---|---:|---:|---:|---|
| `afternoon-research/` | 目录 | 36 | 2.10 | 下午问候素材研究及脚本，历史研究产物，待人工确认 |
| `afternoon-root-verify/` | 目录 | 117 | 479.69 | 下午问候 Release 验证输出，QA/发布验证产物 |
| `animation-edges-root-build.txt` | 文件 | 1 | 0.00 | 构建记录 |
| `animation-edges-root-final-build.txt` | 文件 | 1 | 0.00 | 构建记录 |
| `archive/` | 目录 | 425 | 60.00 | 历史归档；含 `validation/` 归档，不作为运行时输入 |
| `calendar-source/` | 目录 | 74 | 1.66 | 日历源数据生成/验证产物 |
| `capture-music-character.ps1` | 文件 | 1 | 0.00 | 音乐角色截图脚本 |
| `compact-tray-worktree/` | 目录 | 3,243 | 2,363.23 | Git worktree，见下表，不能直接删除 |
| `fishing-shared-build.txt` | 文件 | 1 | 0.00 | 构建记录 |
| `hehe-root-build.txt` | 文件 | 1 | 0.00 | 构建记录 |
| `install-music-fix.ps1` | 文件 | 1 | 0.00 | 音乐修复安装脚本 |
| `maintenance/` | 目录 | 1,075 | 858.64 | 发布清理和项目结构维护验证产物 |
| `message-inbox-root-build.txt` | 文件 | 1 | 0.00 | 构建记录 |
| `msix/` | 目录 | 224 | 2,512.21 | MSIX 发布、签名、安装和 staging 产物 |
| `music-character-fixed.png` | 文件 | 1 | 0.03 | 音乐角色截图 |
| `music-character.png` | 文件 | 1 | 0.07 | 音乐角色截图 |
| `music-fix-worktree/` | 目录 | 2,994 | 2,577.85 | detached Git worktree，工作区有改动，不能删除 |
| `music-metadata-build/` | 目录 | 162 | 36.36 | 音乐元数据 Release 构建产物 |
| `music-settings-feedback-worktree/` | 目录 | 3,182 | 2,759.41 | Git worktree，所在分支未合并，不能删除 |
| `music-visual-qa/` | 目录 | 108 | 304.74 | 音乐视觉 QA 输出 |
| `notification-icons-root-build.txt` | 文件 | 1 | 0.00 | 构建记录 |
| `notification-probe/` | 目录 | 138 | 1.98 | 通知探针项目及 bin/obj/验证输出 |
| `portable/` | 目录 | 1,273 | 1,093.11 | 便携包 release、staging 和清理验证 |
| `preserve-root-build.txt` | 文件 | 1 | 0.00 | 构建记录 |
| `probe-music-fixed.ps1` | 文件 | 1 | 0.00 | 音乐探针脚本 |
| `probe-music.ps1` | 文件 | 1 | 0.00 | 音乐探针脚本 |
| `qa/` | 目录 | 104 | 445.83 | 综合 QA 截图、WebP 实验和音量验证 |
| `qa-long-idle-current/` | 目录 | 126 | 617.86 | 当前版本长待机发布/验证输出 |
| `qa-long-idle-net10/` | 目录 | 110 | 479.07 | 历史 net10 长待机发布输出；最终目标已收敛到 net48 |
| `qa-long-idle-net48/` | 目录 | 107 | 304.78 | net48 长待机发布/验证输出 |
| `quick-actions/` | 目录 | 233 | 785.30 | 快捷操作构建和验证输出 |
| `recycle-root-build.txt` | 文件 | 1 | 0.00 | 构建记录 |
| `sideload/` | 目录 | 9 | 399.10 | 旁加载包 release/staging |
| `size-audit/` | 目录 | 97 | 171.57 | 包体积审计和 WebP 候选实验 |
| `tray-fit-root-build.txt` | 文件 | 1 | 0.00 | 构建记录 |
| `tray-fix-worktree/` | 目录 | 3,048 | 2,239.76 | detached Git worktree，工作区有改动，不能删除 |
| `tray-narrow-root-build.txt` | 文件 | 1 | 0.00 | 构建记录 |
| `wechat-timing-root-build.txt` | 文件 | 1 | 0.00 | 构建记录 |

## 历史 worktree 逐项判断

| 路径 | 分支/状态 | 工作区 | 与 `main` 的关系 | 结论 |
|---|---|---|---|---|
| `artifacts/compact-tray-worktree` | `codex/compact-tray-menu` | 干净 | 分支有 3 个仅分支提交，未合并；`main` 另有 40 个提交 | 不可安全删除，需先决定是否保留或处理分支 |
| `artifacts/music-fix-worktree` | detached，HEAD `7dcf468` | 有改动：2 个源码/测试文件，77 行差异 | detached，不能用分支合并关系代替保留判断 | 不可安全删除，先处理未提交改动 |
| `artifacts/music-settings-feedback-worktree` | `codex/update-animation-picker-20260912` | 干净 | 分支有 1 个仅分支提交，未合并；`main` 另有 55 个提交 | 不可安全删除，需先决定是否保留或处理分支 |
| `artifacts/tray-fix-worktree` | detached，HEAD `292cfc4` | 有改动：已暂存修改/新增及未跟踪文件 | detached，包含文档、源码、脚本和 QA 文件改动 | 不可安全删除，先处理工作区内容 |

## 其他未合并分支

只读检查发现以下本地分支未被 `main` 吸收，不能因目录看起来像历史产物而直接删除：

| 分支 | 备注 |
|---|---|
| `codex/compact-tray-menu` | 对应保留的紧凑托盘 worktree |
| `codex/update-animation-picker-20260912` | 对应保留的音乐设置反馈 worktree |
| `mode1-disable-file-drop-20260912` | 对应本机隔离工作区下保留的 worktree |
| `codex/fix-recycle-animation-direction` | 有远端同名分支，含回收站方向修复提交 |
| `codex/organize-project-files-20260912` | 有远端同名分支，含项目整理提交 |
| `feedback-reduction-20260912` | 有远端同名分支，含反馈去重提交 |
| `spin-dance-08x-20260912` | 有远端同名分支，含旋转舞速度调整提交 |

## 发布物、QA 和其他产物结论

| 类别 | 路径 | 结论 |
|---|---|---|
| 发布物 | `msix/`、`portable/`、`sideload/` | 保留到发布包分析、最终验收及用户确认清理后再处理 |
| QA/验证 | `qa/`、`qa-long-idle-*`、`quick-actions/`、`music-visual-qa/`、`afternoon-root-verify/`、`notification-probe/` | 先保留；可在最终验收阶段按精选证据规则归档或由用户确认删除 |
| 构建/探针 | `maintenance/`、`music-metadata-build/`、`calendar-source/`、根目录构建记录及探针脚本 | 先保留；需按用途和是否已纳入文档逐项清理 |
| 历史研究/归档 | `afternoon-research/`、`archive/`、`size-audit/` | 不影响运行时；暂不删除，后续由用户确认是否清理 |
| Git worktree | 4 个 `*-worktree/` | 当前均不可安全删除，详见逐项表 |

## 阶段结论

Module C 审计完成。当前没有可以在本模块直接判定并执行删除的对象；本模块不执行删除。按原始需求，下一步进入阶段 3「拆分启动组合根」。
