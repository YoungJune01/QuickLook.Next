# 便携版

## 使用

下载本仓库 [GitHub Releases](https://github.com/YoungJune01/QuickLook.Next/releases)
中的 `QuickLook.Next-<版本>-portable-win-x64.zip`。后续更新源也使用该仓库，
不会从原作者仓库获取本分支的更新。感谢原作者 SherlockChiang 和上游贡献者，
完整声明见 [ACKNOWLEDGEMENTS.md](../ACKNOWLEDGEMENTS.md)。

将完整 ZIP 解压到可写的本地文件夹，先退出其他 QuickLook 实例，再双击
`Start.cmd`。适用 Windows x64；无需管理员权限、安装 .NET/SDK、商店认证或证书。
不要直接在压缩包内启动，也不要只复制 EXE。

| 目录 | 内容 |
|---|---|
| `App/` | 应用、三个宿主、Windows App SDK 和 Visual C++ 运行库 |
| `Runtime/` | 包内共享 .NET 运行时，不含开发 SDK |
| `Data/` | 设置、欢迎状态、日志、PDF 缓存、临时文件和宿主工作文件 |
| `Licenses/` | 随包运行库的再分发说明；其他许可随根目录和 Runtime 保留 |

通过托盘菜单退出后，可移动整个目录或复制到其他电脑；携带 `Data` 即保留设置。
退出后可删除 `Data/Cache` 和 `Data/Temp`；删除整个 `Data` 会重置设置。
便携模式默认不开机自启；可在托盘或设置中手动开启，为当前目录创建独立的
当前用户启动项，不影响其他安装。移动或删除目录前请先关闭自启；启动项不会自动迁移。
自启通过包内 `Start.ps1` 配置子进程运行环境，不修改系统环境或全局脚本策略。
目录不可写或 Data 被链接到
外部时拒绝启动，不回退到用户目录。`portable.mode` 是便携模式标记，请保留。

Windows 自身可能保留运行历史、崩溃报告和缩略图缓存；可选解码器仍取决于目标系统。
主动打开原文件、浏览器或另存文件属于用户选择的外部操作。

## 重新打包

GitHub 自动发布流程见 [github-release.md](github-release.md)。以下为本地打包入口。

先完成 `AGENTS.md` 中的完整构建与验证，在同一源代码版本上记录构建证明，
然后指定本机已准备好的 .NET 10 x64 运行时和可再分发的 Visual C++ x64 文件：

```powershell
pwsh -NoProfile -File tools/write-tested-release-proof.ps1 -VersionPrefix (Get-Content VERSION -Raw).Trim()
pwsh -NoProfile -File tools/pack-portable.ps1 `
  -RuntimeRoot 'C:\toolchains\dotnet' `
  -VCRuntimeDirectory 'C:\redist\Microsoft.VC143.CRT' `
  -VCRedistributionNotice 'C:\redist\Redist.txt'
```

输出到 `artifacts/QuickLook.Next-<VERSION>-portable-win-x64/`，同时生成 ZIP 和
SHA-256。输出必须是新目录，避免覆盖已有用户数据；整包解压大小预算为 350 MiB。
原 `pack-release.ps1` 仍是基础发布材料组装器；完整便携交付使用 `pack-portable.ps1`。
用 `tools/test-portable-package.ps1 -PackageDirectory <输出目录>` 验证复制后的包；
测试在独立目录中执行，不将测试设置和日志写回交付目录。
