QuickLook Next - Windows x64 Portable

Extract the complete ZIP to a writable local folder, then double-click Start.cmd.
Do not run from inside the ZIP or copy only the EXE. Exit other QuickLook
instances before starting. No installation, administrator account, Store login,
.NET installation, certificate import, or SDK installation is needed.

App/       Application and isolated preview hosts, WinUI and Visual C++ files.
Runtime/   Bundled .NET runtime; no developer SDK, Cargo, or PowerShell required.
Data/      Settings, welcome state, logs, PDF cache, temporary files, host work.
Licenses/  Third-party runtime redistribution notices.

Use Space to preview the selected desktop/Explorer file. Exit from the tray
before moving or removing the folder. Copy the complete folder including Data
to carry settings to another computer. With the app closed, Data/Cache and
Data/Temp may be deleted; deleting all Data also resets preferences.

Portable startup is off by default. Enable it in the tray menu or Settings to
create a separate current-user startup entry for this folder, using Start.ps1.
Disable it before moving or deleting the folder; entries are not migrated.
It does not modify another installed copy's settings, logs, or startup entries.
Explicit Open File / browser / Save
actions may hand data to other applications or a location chosen by you.
Windows itself may retain execution history, crash reports, thumbnail caches,
or other system-managed data; application portability cannot prevent these.
Optional image/media codecs still depend on the destination Windows system.

便携版使用说明

将 ZIP 完整解压到可写的本地文件夹，双击 Start.cmd 启动。
无需安装 .NET、SDK、微软商店组件或导入证书；不要直接在压缩包内启动。
先退出其他 QuickLook 实例。通过系统托盘菜单退出后，再移动或删除文件夹。
设置、日志、PDF 缓存、应用临时文件和宿主工作目录均位于 Data 内；
复制整个文件夹（包括 Data）即可携带设置。可在托盘或设置中手动开启开机自启；
只为当前目录创建独立的当前用户启动项，移动或删除目录前请先关闭。
Windows 自身的运行记录、崩溃报告、缩略图等系统数据不受应用控制。
部分图片/音视频格式仍依赖目标 Windows 已有的可选解码器。
由 YoungJune01 独立维护，感谢原作者 SherlockChiang 和上游贡献者。
下载和更新：https://github.com/YoungJune01/QuickLook.Next/releases
更新时退出程序，将新版解压到新目录，再复制旧版 Data/ 保留设置。
移动目录前关闭旧版开机自启，在新版中按需重新开启。
请保留 LICENSE、ACKNOWLEDGEMENTS.md、THIRD-PARTY-NOTICES.txt 和 Licenses 目录。
