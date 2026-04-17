# 开发环境与依赖安装（SETUP）

本节说明开发环境所需的依赖和本地配置步骤。

## 前置条件

- .NET SDK：请安装与解决方案兼容的 .NET SDK（建议安装当前 LTS 或项目指定的 SDK）。
- Node.js + npm/yarn：用于 Electron 前端与构建工具。
- PowerShell（Windows 开发脚本依赖）。
- 可选：Playwright CLI（在需要手动管理浏览器运行时时使用）。

## 克隆与构建

```powershell
# 克隆仓库
git clone <repo-url>
cd meowautochrome

# 构建解决方案
dotnet build MeowAutoChrome.slnx
```

## 运行（开发模式）

- 推荐使用仓库提供的 `dev.ps1` 脚本，它通常会协同启动 WebAPI 与 Electron 开发进程：

```powershell
.\dev.ps1
```

- 单独运行 WebAPI：

```powershell
dotnet run --project MeowAutoChrome.WebAPI/MeowAutoChrome.WebAPI.csproj
```

- Electron 开发（视 `package.json` 而定）

```powershell
cd MeowAutoChrome.Electron
npm install
npm run start
```

## Playwright 浏览器安装模式

- 仓库支持在线/离线打包模式（`PackageMode=online|offline|both`）。离线包会包含 `chrome-win64.zip`，用于在无法联网环境下安装浏览器。有关详细脚本请查看 `package-app.ps1` 与 `MeowAutoChrome.Core/Services/PlaywrightRuntimeService.cs`。

## 其他脚本

- `pack.ps1`：构建与打包脚本（发布时使用）。
- `dev.ps1`：开发时一键运行脚本。

## 注意事项

- 确认 `MeowAutoChrome.Contracts` 在插件与主机间的版本一致，避免二进制不兼容。
- 若使用自有 Playwright 运行时安装方式，请参考 `MeowAutoChrome.Core/Services/PlaywrightRuntimeService.cs` 的实现以保证兼容性。
