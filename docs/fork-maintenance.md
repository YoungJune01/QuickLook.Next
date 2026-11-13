# 个人维护起步

本页记录这份下载源码的本地维护流程。架构与安全约束仍以
[`../AGENTS.md`](../AGENTS.md)、[`../CONTRIBUTING.md`](../CONTRIBUTING.md)
及 ADR 为准。

## 源码与版本管理

- 下载包内的 `VERSION` 为 `1.0.4`，原始文件已保存到本地 Git 提交
  `396fbbf`。这是下载快照的备份，不代表已确定其上游提交号。
- 第一轮修改位于 `fix/desktop-and-explorer-tabs` 分支；Excel 预览修复位于
  基于它创建的 `fix/workbook-preview-layout` 分支。
- 大文件超限提示在其后的 `fix/workbook-preview-limits` 分支维护。
- 空白单元格显示修复在其后的 `fix/workbook-empty-cells` 分支维护。
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
`artifacts/local-preview-20261113-workbook-blanks/`，包含 App、宿主、许可及第三方声明，并核对
了复制前后的文件哈希。退出其他 QuickLook 实例后，双击
`artifacts/Start-MyQuickLook.cmd` 启动。本地入口与测试目录均不提交到 Git。
重新编译后需要重新组装测试目录；它不会自动使用新的编译输出。
原来的 `artifacts/local-preview-20261113/` 保留为第一轮修复的回退版本。
`artifacts/local-preview-20261113-workbook/` 保留为工作表布局修复的回退版本。
`artifacts/local-preview-20261113-workbook-limits/` 保留为超限提示修复的回退版本。

## 第一轮问题与验收

1. **桌面无法预览**：原有快捷键过滤只接受普通 Explorer 窗口，选中项枚举
   也没有使用桌面 Shell 接口。
2. **多标签页读取首标签页**：不同标签页共用主窗口句柄；只匹配主窗口会读到
   第一个 COM 对象。需进一步匹配当前标签页的 Shell 浏览器句柄。

自动化测试、原生接口实测、完整 App 界面验收分别记录，不互相替代。
第一轮 Release 构建为零警告、零错误，371 项 Rust 测试、546 项 .NET 测试及完整
架构检查已通过；同窗口两个标签页的 TXT 空格预览已实测标题、正文正确。
用户已确认桌面空格预览及多标签页生效；反复切换和当前页无选中项的完整界面
验收仍独立记录。第一轮证据见
[`review-readiness.md`](review-readiness.md#2026-11-13-local-fork-desktop-and-explorer-tabs)。

## Excel 预览修复与验收

第二轮针对宽 Excel 行高被压缩和多个工作表堆叠：保持表格正常比例，以横向
滚动查看宽表，工作表改为底部单页切换，并使用文件中的名称和顺序。
375 项 Rust 测试、554 项 .NET 测试和 Release 构建已通过。合成宽表的原生
接口实测保留 15 磅行高、中文名称、非连续编号和空白工作表。
用户已确认本轮 Excel 预览可用。后续由维护者完成实现、构建及自动化检查，
实际界面和使用效果由用户验收，不再自动操作用户桌面。详细记录见
[`review-readiness.md`](review-readiness.md#2026-11-13-local-fork-readable-workbook-preview-and-sheet-navigation)。

## 后续维护

大文件提示修复将解压后超过上限的工作表、共享字符串等 XML 与空白内容区分，
显示“文件过大，无法预览”，可点击“打开文件”查看完整数据。工作表及共享字符串
各保留 16 MiB 的解压后大小限制，不以五万行作为拒绝阈值；体积较小的五万行
工作簿仍可预览前 48 行。已只读确认用户报告的文件内部工作表约 69.5 MiB，
触发该限制；新原生接口对原文件返回明确的超限状态，用户已确认提示生效。

空白格修复保留表格中没有文字的单元格及空白合并区域，避免右侧待填写区域
失去网格。只保留文件中实际存在的单元格和合并区域，仍限制在原有的预览范围内。
用户原文件只读检查，私人内容不提交到仓库；本轮界面效果仍由用户验收。

交互操作见 [`desktop-regression-checklist.md`](desktop-regression-checklist.md)。
每个后续问题记录复现步骤、预期/实际结果、最小测试样本和验证结果，完成一个
问题后再提交一个范围清晰的修复。正式分发前单独处理个人版本名称、包身份、
签名及更新来源，并保留仓库要求的许可和第三方声明。
