# 发布计划与检查表（RELEASE_PLAN）

下面为一次标准的发布/打包检查表，适用于桌面应用与后端服务的联合发布。

## 发布前准备

- 合并所有必要的 PR，完成代码审查与 CI 校验。
- 更新 `CHANGELOG.md` 与版本号（可在 `AssemblyInfo` 或项目文件中定义）。
- 确认构建环境与密钥（签名、证书）可用且在 CI 中正确配置。

## 构建与打包步骤

1. 本地/CI 构建：

   ```powershell
   dotnet build -c Release MeowAutoChrome.slnx
   ```

2. 生成 WebAPI 发布包（如果单独部署）：

   ```powershell
   dotnet publish MeowAutoChrome.WebAPI/MeowAutoChrome.WebAPI.csproj -c Release -o ./publish/WebAPI
   ```

3. 打包 Electron 应用：使用仓库中的 Electron 打包配置（`electron-builder.json` / `Artifact/Electron` 等），或调用脚本：

   ```powershell
   .\pack.ps1
   ```

4. 验证打包产物：安装测试、自动化测试运行、快速烟雾测试。

## 验证清单

- 应用能在目标平台上正确启动并连接到后端。
- 插件发现与加载工作正常。
- Playwright 浏览器在目标环境下可成功启动（若包含离线浏览器包，验证离线安装流程）。
- 日志与诊断信息完整。

## 发布与标签

- 在版本控制中打 tag（例如 `vX.Y.Z`）并在发布说明中填充 `CHANGELOG.md` 的要点。
- 将打包产物上传到制品库或发布页面（GitHub Releases、私有文件服务器等）。

## 回滚计划

- 保留可回滚的产物与部署脚本，确保能回退到上一版本（保留上一版本 artifact 与数据库迁移备份）。

## 自动化建议

- 把上述流程尽可能纳入 CI/CD（构建、签名、上传、测试、发布）。

参考脚本：仓库根目录的 `dev.ps1` / `pack.ps1`。
