# 贡献指南（CONTRIBUTING）

感谢你愿意为本项目贡献代码。以下为建议的流程与规范。

## 分支与提交规范

- 使用功能分支（feature/*）、修复分支（fix/*）或文档分支（docs/*）。
- 提交信息建议采用 `type(scope): subject` 风格，例如 `feat(core): add runtime status endpoint`。
  - 常见 type：`feat`, `fix`, `docs`, `chore`, `refactor`, `test`。

## 提交前检查

- 本地构建通过：`dotnet build MeowAutoChrome.slnx`。
- 如果修改了前端，确保 `MeowAutoChrome.Electron` 的构建/运行无误。
- 添加或修改后端接口时请同步更新 `docs/API.md` 或 Swagger 文档（如启用）。

## 代码风格

- 遵循仓库现有风格与 .NET 社区常用风格（命名、异常处理、依赖注入使用等）。
- 为新增公共 API 或复杂逻辑编写单元测试（如适用）。

## PR 流程

1. Fork 并在 feature 分支上提交变更。
2. 发起 Pull Request（PR），在描述中包含变更摘要、测试方式与影响范围。
3. 等待代码审查；根据审查意见修改并补充测试或文档。

## 代码所有权与发布

- 重要变更需通知维护者并在 Release Plan 中记录影响（兼容性、迁移步骤）。

## 参与约定

- 在公共仓库提交前，请确保不包含敏感信息或未授权的凭据。
- 对外部依赖的引入请评估安全性与许可。

感谢你对项目的贡献 —— 清晰的提交与详尽的 PR 描述会大大加快合并流程。
