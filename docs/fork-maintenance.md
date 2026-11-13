# 个人维护起步

本页记录这份下载源码的本地维护流程。架构与安全约束仍以
[`../AGENTS.md`](../AGENTS.md)、[`../CONTRIBUTING.md`](../CONTRIBUTING.md)
及 ADR 为准。

## 源码与版本管理

- 下载包内的 `VERSION` 为 `1.0.4`，原始文件已保存到本地 Git 提交
  `396fbbf`。这是下载快照的备份，不代表已确定其上游提交号。
- 第一轮修改位于 `fix/desktop-and-explorer-tabs` 分支。
- 原有 `AGENTS.md` 保持不变。构建产物、工具下载和测试证据放在已忽略的
  `artifacts/`，不提交证书、日志及私人样本。
- 建立自己的 GitHub Fork 后，应从 Fork 克隆保留上游历史的正式工作副本，
  再迁移本轮修复。当前本地仓库尚未关联远程，也没有发布任何代码。

查看修改及原始备份：

```powershell
git status --short --branch
git diff
git diff main...HEAD
git show 396fbbf:VERSION
```

## 本机开发入口

本次准备了固定版本 .NET SDK `10.0.302`、Rust `1.96.0`、PowerShell `7.5.4`
和本地 Windows SDK。`.NET`、PowerShell 及 Windows SDK 位于
`artifacts/tools/`，没有修改项目的工具链版本与依赖锁文件。

在仓库根目录打开 PowerShell，先载入本机生成的环境脚本，再使用原有入口：

```powershell
. .\artifacts\tools\Enter-Dev.ps1
pwsh -NoProfile -File .\build.ps1 -Test
```

环境脚本只对当前进程设置工具路径和编译器搜索路径，不是随仓库分发的文件。
换电脑时按 `CONTRIBUTING.md` 准备环境。Release App 运行时需要同次构建的
三个宿主，不能只复制或单独启动 App 编译目录。

本次使用项目已有的 `tools/release-payload.ps1` 组装了本地测试目录
`artifacts/local-preview-20261113/`，包含 App、宿主、许可及第三方声明，并核对
了复制前后的文件哈希。退出其他 QuickLook 实例后，双击
`artifacts/Start-MyQuickLook.cmd` 启动。本地入口与测试目录均不提交到 Git。
重新编译后需要重新组装测试目录；它不会自动使用新的编译输出。

## 第一轮问题与验收

1. **桌面无法预览**：原有快捷键过滤只接受普通 Explorer 窗口，选中项枚举
   也没有使用桌面 Shell 接口。
2. **多标签页读取首标签页**：不同标签页共用主窗口句柄；只匹配主窗口会读到
   第一个 COM 对象。需进一步匹配当前标签页的 Shell 浏览器句柄。

自动化测试、原生接口实测、完整 App 界面验收分别记录，不互相替代。
本轮 Release 构建为零警告、零错误，371 项 Rust 测试、546 项 .NET 测试及完整
架构检查已通过；同窗口两个标签页的 TXT 空格预览已实测标题、正文正确。
桌面空格预览、反复切换和当前页无选中项的完整界面验收仍待完成，具体证据见
[`review-readiness.md`](review-readiness.md#2026-11-13-local-fork-desktop-and-explorer-tabs)。
交互操作见 [`desktop-regression-checklist.md`](desktop-regression-checklist.md)。
每个后续问题记录复现步骤、预期/实际结果、最小测试样本和验证结果，完成一个
问题后再提交一个范围清晰的修复。正式分发前单独处理个人版本名称、包身份、
签名及更新来源，并保留仓库要求的许可和第三方声明。
