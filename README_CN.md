# QuickLook Next

[English](README.md) · [本地维护指南](docs/fork-maintenance.md)

这是基于上游 `1.0.4` 源码快照维护的 Windows x64 文件预览器。
在桌面或资源管理器当前标签页选中文件，按 **空格键** 预览。
桌面、多标签页以及 Excel 布局、超限提示和空白格修复均已通过用户核心场景验收。

## 启动与开发

本机双击 `artifacts/Start-MyQuickLook.cmd` 启动，先退出其他 QuickLook 实例。
该入口使用独立的完整测试包，重新编译源码不会自动更新它；回退方法见维护指南。

需要 Windows、固定版本 .NET SDK、Rust MSVC 工具链、Desktop C++ 与 Windows SDK，
具体要求见 [CONTRIBUTING.md](CONTRIBUTING.md)。在仓库根目录运行：

```powershell
# 本机先载入开发环境；换电脑需自行准备工具链
. .\artifacts\tools\Enter-Dev.ps1
pwsh -NoProfile -File tools/doctor.ps1
pwsh -NoProfile -File build.ps1 -Test
```

完整验证、不签名或安装：

```powershell
pwsh -NoProfile -File tools/release.ps1 -SkipPackage
```

`tools/pack-release.ps1` 可组装便携 ZIP，包含 App、三个宿主、许可和第三方声明。
`packaging/` 与相关脚本保留可选的签名 MSIX 侧载安装能力，需要自己的签名配置。
日常本地运行不需要商店认证、Partner Center 配置或导入证书。

## 功能与快捷键

- 图片与动画、PDF、DOCX/XLSX/PPTX 近似版式。
- 文本、代码、Markdown、CSV/TSV、SQLite、压缩包、软件包和文件夹。
- 系统支持的音视频、字体、证书、可执行文件、邮件和电子书。
- 云端下载确认、取消、受限解析及隔离宿主。
- 空格打开或关闭，`Esc` 关闭，`F5` 重载，`F11` 全屏。
- 图片滚轮或 `+`/`-` 缩放，`Home` 或 `Ctrl+0` 复位，`Ctrl+F` 搜索文本。

Excel 在底部切换工作表，保持正常行高、空白格及合并区域；预览最多保留前 48 行、
32 列、6 张工作表。工作表 XML 超限时显示友好提示，可打开原文件查看完整数据。
Office 预览不会执行宏或重算公式，复杂版式可能与 Office 不同。
部分图片及音视频格式依赖 Windows 可选解码器。

## 维护与目录

`src/` 包含 WinUI 外壳、共享模型及 ParserHost、RasterHost、ShellBroker；
`native/quicklook_next_native/` 负责格式探测与受限结构化解析；
`tests/`、`testdata/`、`tools/` 提供验证、样本和工具。
已移除的网站、旧插件及历史发布材料可通过 Git 查阅。

[AGENTS.md](AGENTS.md) 和 [ADRs](docs/adr/README.md) 定义工程边界。
实现状态见[验证记录](docs/review-readiness.md)，界面操作见[回归清单](docs/desktop-regression-checklist.md)，
后续计划见[维护待办](docs/implementation-todo.md)。反馈问题请提供提交号、文件类型及大小、
操作步骤、预期与实际结果；优先制作合成样本，不提交私人文件。

## 安全与许可

私密漏洞报告按 [SECURITY.md](SECURITY.md) 处理，贡献要求见 [CONTRIBUTING.md](CONTRIBUTING.md)。
原创源码和资产使用 [MIT License](LICENSE)，第三方依赖保留各自许可和声明。
上游项目为 `SherlockChiang/QuickLook.Next`。
