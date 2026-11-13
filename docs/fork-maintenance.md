# 本地维护指南

这份源码从下载的 `1.0.4` 快照开始维护。本页提供目录、开发和回退入口；
实现与验证状态以 [review-readiness.md](review-readiness.md) 为准，贡献流程见
[CONTRIBUTING.md](../CONTRIBUTING.md)，架构规范见 [AGENTS.md](../AGENTS.md)。

## 当前使用入口

- 双击本机的 `artifacts/Start-MyQuickLook.cmd`，当前启动
  `artifacts/local-preview-20261113-workbook-blanks/`。先退出其他 QuickLook 实例。
- 当前功能基线为 `38271f4`，保留于 `fix/workbook-empty-cells`；下表修复依次叠加。
  项目精简工作位于其后的 `chore/slim-local-maintenance` 分支。
- 2026-11-13，用户已确认全部已报告核心场景通过。反复切换、无选中项、主题等
  组合场景仍按[交互回归清单](desktop-regression-checklist.md)单独记录。
- 最新代码验证基线：Rust 390 项通过、1 项原有忽略；.NET 557 项通过；Release
  构建零警告、零错误；格式、Clippy 和完整架构检查通过。
  项目精简后的独立验证结果见 [review-readiness.md](review-readiness.md)。

## 已完成修复

| 问题 | 核心验收结果 | 修复提交 / 保留分支 |
|---|---|---|
| 桌面空格预览、Explorer 多标签页 | 桌面可预览，读取当前标签页选中项 | `f574bf7` / `fix/desktop-and-explorer-tabs` |
| Excel 行高、工作表布局 | 宽表正常行高并横向滚动，底部 Sheet 切换 | `008b0b3` / `fix/workbook-preview-layout` |
| Excel 超限提示 | 显示“文件过大，无法预览”和打开文件入口 | `ac931f4` / `fix/workbook-preview-limits` |
| Excel 空白区域缺失 | 保留实际空白格、空白合并区域的网格及已有样式 | `38271f4` / `fix/workbook-empty-cells` |

Excel 仍按现有预算预览前 48 行、32 列、最多 6 个工作表。工作表和共享字符串
XML 各有 16 MiB 的解压后大小限制；不以“五万行”作为拒绝阈值。预览不是完整的
Excel 编辑器，查看全部数据请打开原文件。私人样本未提交到仓库。

## 项目目录与文档

| 路径 | 用途 |
|---|---|
| `native/quicklook_next_native/` | Rust 格式探测、受限解析和结构化预览模型 |
| `src/` | WinUI App、Core、通信契约及三个隔离宿主 |
| `tests/` | Core 单元测试和宿主集成测试；Rust 测试随原生模块维护 |
| `testdata/` | 合成或获准分发的回归样本 |
| `tools/`、`packaging/` | 构建、检查、发布脚本与打包配置 |
| `docs/` | 实现状态、验收清单、ADRs、格式说明及后续计划 |
| `artifacts/` | 本机工具、测试包和证据；已被 Git 忽略 |

[review-readiness.md](review-readiness.md) 区分本地修复与上游历史验证，
[docs/adr/README.md](adr/README.md) 索引已接受的架构决策，
[handle-based-preview-inputs.md](handle-based-preview-inputs.md) 记录 HANDLE/ABI 细节，
[implementation-todo.md](implementation-todo.md) 维护精简后的当前待办。

商店上架流程、宣传网站、未参与构建的旧插件和冗长历史文档已移出工作目录，
清理前的完整版本保留于 Git 提交 `54302d0`。例如：

```powershell
git show 54302d0:docs/implementation-todo.md
git ls-tree -r --name-only 54302d0 -- website plugins
```

当前运行代码、格式支持、测试、安全检查和必要 Windows 资源保留。
`packaging/` 中的可选侧载安装与商店上架独立；其签名、证书校验属于 Windows
安装机制，日常本地启动无需使用。`StoreLogo.png` 是安装清单引用的图标，仍保留。

## 本机开发与验证

在仓库根目录打开 PowerShell：

```powershell
. .\artifacts\tools\Enter-Dev.ps1
pwsh -NoProfile -File .\tools\doctor.ps1
pwsh -NoProfile -File .\build.ps1 -Test
```

本机脚本为当前进程配置固定版本 .NET SDK `10.0.302`、Rust `1.96.0`、
PowerShell `7.5.4`、MSVC 和 Windows SDK；换电脑时按 `CONTRIBUTING.md` 准备环境。
原生 DLL 单独构建须走 MSBuild：

```powershell
dotnet msbuild native\QuickLook.Next.Native.proj -target:Build -verbosity:minimal
```

按改动先跑针对性回归，再跑 `AGENTS.md` 中的相关完整检查。
重新编译不会更新独立测试包，需重新组装；现有本机组装脚本为
`artifacts/assemble-workbook-blank-cells-preview.ps1`，使用项目的
`tools/release-payload.ps1` 并验证复制哈希。App 必须与同次构建的三个宿主一起运行。
实现、构建与自动化检查由维护者完成，实际界面由用户验收。

## 版本管理与回退

`main` 保留原始快照 `396fbbf`，对应上游提交尚未确定。当前只有本地 Git，
没有关联远程、建立 GitHub Fork 或发布个人版本。

```powershell
git status --short --branch
git log --oneline --decorate -6
git diff main...HEAD
```

以下测试目录与同名 `.manifest.json` 均保留，不移动或删除：

| `artifacts/` 下的目录 | 版本用途 |
|---|---|
| `local-preview-20261113/` | 桌面与多标签页修复 |
| `local-preview-20261113-workbook/` | Excel 行高与 Sheet 切换 |
| `local-preview-20261113-workbook-limits/` | 超限友好提示 |
| `local-preview-20261113-workbook-blanks/` | 当前使用版，含空白格修复 |

需要回退时，退出正在运行的实例，再从对应目录启动 `QuickLook.Next.App.exe`；
保持目录内文件完整。详细本地产物索引见本机 `artifacts/README.md`。

## 后续维护顺序

1. 新问题先记录操作、预期、实际结果，优先制作不含个人数据的最小复现样本。
2. 一个问题一个修复提交，同步更新实现状态、回归清单和本地测试包，再由用户验收。
3. 补充已有清单中的反复切换、取消及错误恢复等边界场景，未测项保留待验证。
4. 需要异地备份或跟随上游时，从自己的 GitHub Fork 克隆正式副本，再迁移本地修复。
5. 正式分发前明确个人版本名称、包身份、签名和更新来源，保留许可与第三方声明。
