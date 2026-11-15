# GitHub 发布管理

源码仓库：<https://github.com/YoungJune01/QuickLook.Next>

下载地址：<https://github.com/YoungJune01/QuickLook.Next/releases>

上游来源：<https://github.com/SherlockChiang/QuickLook.Next>

## 日常管理

源码与锁文件进入 Git。构建缓存、工具链、签名私钥、日志、用户 Data、私人样本和
ZIP 留在本地；完整软件包通过 GitHub Releases 分发。主分支为 `main`。
普通修改不会自动发布软件；CI 会在 main 推送和 Pull Request 时执行检查。
当前暂停新功能开发，未验证场景继续保留在原有验证记录与待办中。

## 便携版发布

1. 在本地修改 `VERSION` 时使用 `tools/set-version.ps1 -Version X.Y.Z`，保持 Rust
   包版本及锁文件同步。每次正式发布使用新版本号，提交并推送到 `main`。
2. 在 GitHub 的 **Actions → Portable Release → Run workflow** 中选择 `main`。
3. 工作流执行依赖审计、固定工具链构建、全部发布检查、便携打包及运行时/存储检查。
4. 通过后发布 `vX.Y.Z`，上传便携 ZIP、对应 `.sha256` 和 `update.json`，设为最新稳定版。
   已有同名 Release 不会被覆盖；已有标签指向其他提交时会拒绝发布。

便携发布不需要 `QUICKLOOK_RELEASE_PFX_*`，也不需要导入原作者证书。
包内包含运行时、许可、第三方声明及原作者致谢，交付时 `Data/` 必须为空。
`windows-2025` runner 必须提供匹配的 Visual C++ 可再分发文件与声明，缺失时构建失败，
不得用调试 DLL、系统目录 DLL 或删除许可检查代替。

原有 `Release` / `Beta Release` 工作流是可选的签名 MSIX 路径，保留原有安全检查；
使用前须按自己的证书、包身份和 GitHub Environment 配置签名。这不是便携发布的前置条件。
普通提交不要以 `release:` 开头，该前缀仍保留给签名稳定版流程。

## 更新源

应用的仓库、下载页与检查更新地址统一在
`src/QuickLook.Next.Core/ProjectRepository.cs`。当前更新地址为：

```text
https://github.com/YoungJune01/QuickLook.Next/releases/latest/download/update.json
```

便携发布脚本会核对 `Directory.Build.props` 的仓库地址；元数据只能指向本仓库相同版本
的发布资产。客户端保留 HTTPS、精确仓库/文件/标签匹配、16 KiB、深度及超时限制，
不会回退读取上游更新源。签名安装包与便携包文件名都受严格验证。

检查更新仅比较版本并提示，下载与替换由用户手动完成。便携升级时先退出应用，
将新版完整解压到新目录，再复制旧版 `Data/`；移动前关闭旧目录自启，新版按需开启。
`update.json` 中的哈希用于资产一致性信息，当前客户端不会自动下载或安装。

`v1.0.4` 已于 2026-11-15 发布，包含便携 ZIP、SHA-256 文件和 `update.json`。
源码推送与二进制发布分别管理；后续修改源码后仍需运行发布工作流。
以后迁移仓库需要同时修改 `ProjectRepository.cs`、`Directory.Build.props` 与文档，
重新构建客户端，并在旧仓库给现有用户提供迁移说明。
