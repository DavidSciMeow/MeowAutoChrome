# MeowAutoChrome Electron Shell

说明：这个文件夹包含 MeowAutoChrome 的 Electron 桌面前端，用于在本地启动或连接 `MeowAutoChrome.WebAPI` 并加载 renderer UI。

快速开始（开发）：

1. 安装依赖：

```bash
cd MeowAutoChrome.Electron
npm install
```

2. 运行（需要本机安装 .NET SDK）：

```bash
cd MeowAutoChrome.Electron
npm run start
```

默认情况下，Electron 会尝试通过 `dotnet run --project ../MeowAutoChrome.WebAPI/MeowAutoChrome.WebAPI.csproj` 启动后端并等待 `/health` 可达，然后打开窗口加载 `http://127.0.0.1:5000`。

可选：你可以先手动运行 WebAPI（`dotnet run --project MeowAutoChrome.WebAPI`），然后运行 Electron：`npm start`。

打包：使用 `electron-builder`（需先安装并配置签名证书以发布 macOS/Windows 签名版）。
