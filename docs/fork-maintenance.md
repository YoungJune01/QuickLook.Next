# 仓库维护指南

本仓库基于 SherlockChiang/QuickLook.Next 的 `1.0.4` 源码快照独立维护，维护者为
YoungJune01。项目来源与致谢见 [ACKNOWLEDGEMENTS.md](../ACKNOWLEDGEMENTS.md)。
当前暂停新功能开发，保留源码、构建验证和后续修复能力。

## 文件保留范围

| 路径 | 内容与保留原因 |
|---|---|
| `src/QuickLook.Next.App` | WinUI 窗口、设置、输入与预览生命周期 |
| `src/QuickLook.Next.Core`、`Contracts` | 共享逻辑、版本/更新验证及通信契约 |
| `src/*Host`、`ShellBroker` | 隔离解析、Windows 表面生产和 Shell 兼容边界 |
| `native/` | Rust 模块、ABI 定义、固定依赖与 MSBuild 构建契约 |
| `tests/`、`testdata/` | 回归测试与来源明确的样本；停止开发后仍用于验证发布 |
| `tools/` | 构建、架构/安全检查、版本与发布脚本；已有检查相互引用，应整体保留 |
| `packaging/` | 便携启动器，以及可选 MSIX 侧载文件 |
| `.github/` | CI、依赖更新和发布自动化 |
| `docs/` | ADR、HANDLE/ABI、安全边界、验证记录和维护待办 |
| 根目录配置与锁文件 | 固定工具链、版本、构建入口、依赖可重复性与许可 |

旧网站、商店上架流程和参考插件已从工作目录移除，其历史仍在 Git 中。
测试和检查脚本约占很小空间，保留它们有助于后续安全维护，不影响软件运行包大小。

## 本地文件与公开源码

`artifacts/`、`bin/`、`obj/`、`native/target/` 等不属于公开源码，由 `.gitignore`
排除；本机工具、历史测试包、日志和包含设置的 `Data/` 不上传。
本次整理将本地工具与历史材料归档到项目目录之外，归档位置写在本机清理记录中。
本地归档不属于从 GitHub 克隆后所需的项目文件。

软件使用本仓库 Releases 的完整便携 ZIP；不能用源码 ZIP 直接运行。
源码、发布与更新配置见 [GitHub 发布管理](github-release.md)。

## 构建与验证

新电脑按 [CONTRIBUTING.md](../CONTRIBUTING.md) 准备固定工具链。
在仓库根目录执行：

```powershell
pwsh -NoProfile -File tools/doctor.ps1
pwsh -NoProfile -File build.ps1 -Test
```

原生 DLL 的独立构建必须使用：

```powershell
dotnet msbuild native\QuickLook.Next.Native.proj -target:Build -verbosity:minimal
```

完整检查以 [AGENTS.md](../AGENTS.md) 和 `tools/release.ps1` 为准。
实现与验证状态在 [review-readiness.md](review-readiness.md)，人工验收在
[desktop-regression-checklist.md](desktop-regression-checklist.md)，未完成事项在
[implementation-todo.md](implementation-todo.md)；暂停开发不代表待办已完成。

## 历史与回退

本地最初源码快照为 `396fbbf`，其对应上游提交未经确认；不要声称拥有完整上游历史。
`54302d0` 保留此前的详细维护文档，后续提交保留桌面、多标签页和 Excel 修复。
使用 `git log`、`git show <提交>:<路径>` 查看历史，不改写已有提交。
程序回退应使用原版本完整 ZIP 和对应 Data 备份，不能混用不同构建的 App 与宿主。
