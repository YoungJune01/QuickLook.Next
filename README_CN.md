# QuickLook Next

[English](README.md) · [下载](https://github.com/YoungJune01/QuickLook.Next/releases) · [致谢](ACKNOWLEDGEMENTS.md)

Windows x64 文件预览工具。在桌面或资源管理器当前标签页选中文件，按 **空格键** 预览。

本版本由 **YoungJune01** 基于上游 `1.0.4` 源码快照独立维护。
感谢原作者 **SherlockChiang** 开源
[QuickLook.Next](https://github.com/SherlockChiang/QuickLook.Next)，
以及所有上游贡献者。本仓库负责自己的源码、发布和更新。
当前暂停新功能开发，后续通过本仓库管理问题与版本维护。

## 下载与使用

从本仓库 [Releases](https://github.com/YoungJune01/QuickLook.Next/releases) 下载
`QuickLook.Next-<版本>-portable-win-x64.zip`，完整解压到可写文件夹，双击
`Start.cmd`。先退出其他 QuickLook 实例。运行时随包携带，设置和缓存位于
`Data/`，无需开发 SDK 或导入签名证书。

“设置 → 关于 → 检查更新”读取本仓库的更新源，提示可用版本；下载与替换由用户
手动完成。更新时先退出旧版，将新版解压到新目录，再复制旧版 `Data/` 保留设置。
移动旧目录前关闭其开机自启，需要时在新版中重新开启。详见[便携版说明](docs/portable.md)。

## 功能与快捷键

- 图片、动画、PDF，以及 DOCX/XLSX/PPTX 近似版式预览。
- 文本、代码、Markdown、CSV/TSV、SQLite、压缩包、软件包和文件夹。
- 系统支持的音视频、字体、证书、可执行文件、邮件和电子书。
- 云文件下载确认、取消、受限解析及隔离宿主。
- 空格打开或关闭，`Esc` 关闭，`F5` 重载，`F11` 全屏。
- 图片滚轮或 `+`/`-` 缩放，`Home` 或 `Ctrl+0` 复位，`Ctrl+F` 搜索文本。

本版本包含桌面与 Explorer 多标签页选择、Excel 行高与 Sheet 切换、空白格与
合并区域、多窗口交互、DOCX 图文顺序、GBK 表格及便携开机自启等修复。
Excel 预览最多保留前 48 行、32 列、6 张工作表；Office 预览不执行宏、不重算
公式，复杂排版为近似呈现。部分图片及音视频格式依赖 Windows 可选解码器。

## 源码与维护

| 路径 | 用途 |
|---|---|
| `src/` | WinUI 外壳、共享模型及隔离预览宿主 |
| `native/` | Rust 格式探测、受限解析及原生接口 |
| `tests/`、`testdata/` | 回归测试及可分发样本 |
| `tools/`、`packaging/` | 构建、验证与打包 |
| `.github/` | CI、依赖更新及发布工作流 |
| `docs/` | 架构决策、验证记录与维护指南 |

在 Windows x64 上按 [CONTRIBUTING.md](CONTRIBUTING.md) 准备固定工具链，然后运行：

```powershell
pwsh -NoProfile -File tools/doctor.ps1
pwsh -NoProfile -File build.ps1 -Test
```

分发流程见 [GitHub 发布管理](docs/github-release.md)，目录与维护方式见
[维护指南](docs/fork-maintenance.md)。[AGENTS.md](AGENTS.md) 与
[ADRs](docs/adr/README.md) 定义工程边界；[验证记录](docs/review-readiness.md) 和
[维护待办](docs/implementation-todo.md) 保留已验证与待验证事项。
本机工具链、日志、构建缓存、私人样本和发布 ZIP 不进入 Git；软件包通过 Releases 分发。

## 许可与反馈

保留原作者版权声明及 [MIT License](LICENSE)，第三方组件保留各自许可证与声明。
项目来源详见 [ACKNOWLEDGEMENTS.md](ACKNOWLEDGEMENTS.md)，
私密漏洞报告见 [SECURITY.md](SECURITY.md)，贡献要求见 [CONTRIBUTING.md](CONTRIBUTING.md)。
