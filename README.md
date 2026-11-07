# Telegram · Windows Island

Telegram 桌面版消息插件，提供新消息通知与会话头像。

需要 Windows Island 0.1.0-rc.6 或更新版本（合约大版本 1）。GitHub Release 提供插件包；默认插件源收录另行生效。仅当 **插件管理 → 发现插件** 中出现作者为 `winiofficial` 的 Telegram 插件后，才可从该来源安装、阅读条款并启用。本仓库发布不表示发现列表已收录。

运行并登录 Telegram 桌面版后开始接收新消息。当前支持 Telegram Desktop x64 7.2.9（Qt 5）与 7.3.3（Qt 6）；其他版本可尝试客户端通知弹窗，或使用 Windows Island 的系统通知来源。停用后停止监听，卸载保留插件设置与数据。插件不上传聊天内容。

连接能力需要默认插件源验证，手动导入 ZIP 不会取得该能力。发布包见 [Releases](https://github.com/WinIOfficial/wini_plugin_telegram/releases)，发布与默认源收录分别生效。

## 构建与发布

在 Windows x64 上安装 .NET 10 SDK、CMake 和 Visual Studio C++ 工具链。CI 使用 Windows Island `v0.1.0-rc.6` 的固定提交 `b2cd42defd6e30d650b1fed562225d7c0a955812`，将宿主源码放在相邻的 `windowsisland` 目录；也可向打包脚本传入 `-ContractsFeed <本地包目录> -ContractsVersion 0.1.0-rc.6`，使用该宿主公开的 Contracts 包。

```powershell
dotnet build tests/Wini.Plugin.Telegram.HookTests -c Release
dotnet run --project tests/Wini.Plugin.Telegram.HookTests -c Release --no-build -- $PWD
dotnet run --project tests/Wini.Plugin.Telegram.HookTests -c Release --no-build -- $PWD --qt6
./packaging/Build-Plugin.ps1 -Version 0.1.0
```

最低宿主版本依据 rc.6 的桌面代理 API 与合约确定，插件版本 `0.1.0` 不要求尚未发布的同名宿主正式版。打包脚本生成根目录含清单、托管入口、原生钩子及许可证的 ZIP、SHA-256 文件与目录条目；拒绝覆盖已有版本。发布前用对应宿主 SDK 的 `wini-plugin validate` 和 `doctor <解压目录> --host-version 0.1.0-rc.6` 检查包，并在独立开发实例核对安装、授权、启停与删除行为。签名提交和 `v0.1.0` 标签后，将 `artifacts` 中的 ZIP、`.sha256` 与 `catalog-0.1.0.json` 上传到同仓库 Release；目录条目是提交平台的元数据，不代表插件已收录。
