# Windows 安装与包身份

QQ / 微信来源提醒使用 Windows `UserNotificationListener`。微软要求调用方同时满足：

- 以 MSIX 安装并取得 package identity；
- 清单声明 `uap3:userNotificationListener` 和桌面应用所需的 `runFullTrust`；
- 使用者在桌宠“设置 → 通知”中亲自批准 Windows 权限。

因此便携 ZIP 和直接运行的普通 EXE 可以使用动画、音乐和文件功能，但永远不能开启通知监听。
这不是 QQ / 微信安装路径差异，也不能通过扫描进程、聊天数据库或窗口内容安全补救。

正式交付统一使用 Windows 自带的 .NET Framework 4.8 运行 WPF，不要求使用者另行下载 .NET。
当前支持范围为 Windows 10 22H2（19045）及 Windows 11。

## 交付方式

- **任意目录安装版**：使用传统安装脚本注册外部位置身份包（sparse package），程序文件保留在用户选择的
  目录中，同时支持 QQ/微信系统通知监听。卸载前询问是否保留 `%LOCALAPPDATA%\LuoTianyiPet`。
- **便携版**：ZIP 解压后直接双击 `LuoTianyiPet.exe`，可以放在任意可写目录。程序通过同目录标记自动
  使用 `UserData`，无需命令行参数；不安装证书、不写注册表，但没有包身份，因此不支持 QQ/微信通知监听。

仓库仍保留完整 MSIX 构建脚本，用于包身份回归和对照测试。完整 MSIX 的安装目录由 Windows 管理，
不作为“任意目录安装版”的用户界面。

构建便携版：

```powershell
powershell -ExecutionPolicy Bypass -File tools\packaging\build_portable_test.ps1
```

输出位于 `artifacts/portable/release/`。当前发布目标固定为 .NET Framework 4.8 x64，.NET 10 SDK
仅作为构建工具使用。

任意目录安装版的构建入口：

```powershell
powershell -ExecutionPolicy Bypass -File tools\packaging\build_external_location_bundle.ps1
```

所有正式打包脚本默认读取 `config/version.props` 中的统一版本；当前正式版本为 `0.1.0.94`。如需重建历史验证包，仍可显式传入 `-Version`，但正式发布不得使用覆盖值。输出位于
`artifacts/external-location/release/`。开发包只把公开 CER 放入安装包，首次安装可能通过一次 UAC
把测试证书加入机器的 `LocalMachine\TrustedPeople`；生产包应使用受信任的代码签名证书，不需要该步骤。

外部位置身份包最低要求 Windows 10 2004（19041）；它不是纯便携方案，安装时仍需注册签名身份包。
参考：[Windows 打包方式](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/packaging/)
和[外部位置包身份](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps)。

当前验证状态：验证版本 `0.0.0.1` 已完成离线构建、清单检查、payload 检查、签名检查和 Windows PowerShell
5.1 语法检查；使用隔离身份 `LuoTianyiPet.ExternalUacTest` 已在 Windows PowerShell 5.1 中实际完成开发证书 UAC 信任、
`Add-AppxPackage -ExternalLocation` 注册、用户选择目录启动、`0.0.0.2` 到 `0.0.0.3` 覆盖升级和旧快捷方式参数清理。卸载保留数据、自启动清理和卸载删除数据沙箱路径均已验证。使用受信任测试证书的隔离身份 `LuoTianyiPet.NotificationTest` 已完成用户授权，应用日志记录 `notification.monitor_status Allowed`；真实 QQ/微信消息读取仍未验证，因此该版本仍不称为正式发布版。PowerShell 7 的 Appx 模块不支持当前平台，验证命令应使用 Windows PowerShell 5.1。

## 完整 MSIX 对照测试

```powershell
powershell -ExecutionPolicy Bypass -File tools\packaging\build_sideload_bundle.ps1
```

输出位于 `artifacts/sideload/release/`：

- `LuoTianyiPet-Installer-<version>-win-x64.zip`；
- 对应的 SHA-256 文件。

测试者完整解压后双击“安装洛天依桌宠.cmd”。该对照路径先校验 MSIX、公钥 CER 的 SHA-256 和
签名者指纹；首次电脑会显示一次 UAC，只把公开开发证书加入
`LocalMachine\TrustedPeople`，随后回到当前登录用户安装 MSIX。桌宠本体不会以管理员权限运行。
安装完成后仍要由使用者在设置页点击“授权访问”。

如果电脑已经安装相同版本且桌宠正在运行，旧脚本会跳过重复注册，而第二个桌宠进程又会被单实例保护
立即结束，所以看起来像“安装没反应”。`0.1.0.37` 起会明确提示“已安装且正在运行”、保留现有进程并
刷新桌面快捷方式；如果人物暂时不在视野内，请查看桌面右下角托盘。

测试包不包含 PFX 私钥或证书密码。自签名证书只适合受控测试，证书过期、签名不一致、包被替换、
试图降级或文件不完整时安装器都会停止。

## 面向公众正式分发

面向公众的任意目录安装版需要使用受信任的生产代码签名证书；开发证书只适合受控测试。
正式签名仍有以下路径可选：

1. 提交 Microsoft Store，由商店使用与 Partner Center 身份一致的证书签名；
2. 使用 Windows 已信任的生产代码签名证书签署身份包和外部位置 EXE。当前脚本支持受信任 CA 签发且可由
   PFX 提供的代码签名证书；Azure Artifact Signing/Trusted Signing 需要另接其远程签名客户端。

外部位置正式安装版的生产构建：

```powershell
powershell -ExecutionPolicy Bypass -File tools\packaging\build_external_location_bundle.ps1 `
  -Version 0.1.0.94 `
  -SigningMode Production `
  -ProductionCertificatePath D:\secrets\luotianyi-production.pfx `
  -ProductionCertificatePasswordPath D:\secrets\luotianyi-production-password.txt `
  -ProductionIdentityName LuoTianyiPet `
  -ProductionPublisherDisplayName 洛天依桌宠
```

完整 MSIX 对照包的生产签名仍可使用原脚本，证书和密码文件必须位于仓库外：

```powershell
powershell -ExecutionPolicy Bypass -File tools\packaging\build_msix.ps1 `
  -Version 0.1.0.94 `
  -SigningMode Production `
  -ProductionCertificatePath D:\secrets\luotianyi-production.pfx `
  -ProductionCertificatePasswordPath D:\secrets\luotianyi-production-password.txt `
  -ProductionIdentityName LuoTianyiPet `
  -ProductionPublisherDisplayName 洛天依桌宠
```

脚本从 PFX 读取真实发布者 Subject 并写入清单，拒绝没有私钥或空密码；发布目录只输出签名
MSIX 与 SHA-256，不复制 PFX、密码或开发 CER。若选择 Microsoft Store，正式包名和 Publisher
必须改为 Partner Center 分配值，不能自行猜测。

## 单独构建开发 MSIX

```powershell
powershell -ExecutionPolicy Bypass -File tools\packaging\build_msix.ps1
```

脚本会创建 .NET Framework 4.8 x64 完整安装布局、生成/复用本机开发证书、打包签名并校验清单能力、关键文件、
签名和 SHA-256。PFX、随机密码和临时发布布局仅位于 Git 忽略的 `artifacts/msix/private/` 与
`artifacts/msix/staging/`。构建脚本本身不安装证书、不注册应用、不申请通知权限。
