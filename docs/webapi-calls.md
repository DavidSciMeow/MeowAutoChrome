# MeowAutoChrome WebAPI 调用文档

## 概览

- 基础地址：`http://127.0.0.1:5000`
- 健康检查：`GET /health`
- 实时通信：`/browserHub`（SignalR）
- 当前后端结构：`MeowAutoChrome.WebAPI` + `MeowAutoChrome.Core`

这份文档以当前 `dev` 分支代码为准，覆盖 Electron 前端实际会调用的后端接口。

## 状态接口

### `GET /api/status`

用途：读取当前浏览器状态、实例信息、标签页、资源占用与实时画面状态。

关键响应字段：

- `currentUrl`：当前活动页面 URL
- `title`：当前活动页面标题
- `supportsScreencast`：当前是否支持实时画面
- `screencastEnabled`：实时画面是否已启用
- `cpuUsagePercent`：CPU 占用百分比
- `memoryUsageMb`：内存占用 MB
- `pluginPanelWidth`：插件区宽度
- `tabs`：标签页列表
- `currentInstanceId`：当前实例 ID
- `currentViewport`：当前实例视口设置
- `isHeadless`：当前运行模式是否为 Headless

## 导航接口

### `POST /api/navigation/navigate`

请求体：

```json
{
  "url": "https://example.com"
}
```

### `POST /api/navigation/back`

请求体：空对象或无请求体。

### `POST /api/navigation/forward`

请求体：空对象或无请求体。

### `POST /api/navigation/reload`

请求体：空对象或无请求体。

## 标签页接口

### `POST /api/tabs/new`

请求体：

```json
{
  "instanceId": "optional-instance-id",
  "url": "https://example.com"
}
```

说明：

- 当 `instanceId` 有值时，后端会先尝试切换到该实例。
- 当当前没有任何实例时，后端会自动创建一个实例。

### `POST /api/tabs/close`

请求体：

```json
{
  "tabId": "tab-id"
}
```

### `POST /api/tabs/select`

请求体：

```json
{
  "tabId": "tab-id"
}
```

## 实例接口

### `GET /api/instances/settings?instanceId={id}`

用途：读取某个实例的设置快照。

### `POST /api/instances/settings`

请求体使用 `Core.Models.BrowserInstanceSettingsUpdateRequest`，当前实际持久化重点是：

- `instanceId`
- `userDataDirectory`
- `viewportWidth`
- `viewportHeight`
- `isHeadless`

### `POST /api/instances`

请求体：

```json
{
  "ownerPluginId": "ui",
  "displayName": "My Instance",
  "userDataDirectory": "optional-path",
  "previewInstanceId": "optional-preview-id"
}
```

### `GET /api/instances/preview?ownerPluginId={owner}&userDataDirectoryRoot={root}`

用途：预览新实例的默认实例 ID 和目录。

### `POST /api/instances/validate-folder`

请求体：

```json
{
  "rootPath": "C:/data/browser-profiles",
  "folderName": "profile-01"
}
```

### `POST /api/instances/close`

请求体：

```json
{
  "instanceId": "instance-id"
}
```

### `POST /api/instances/headless`

请求体：

```json
{
  "isHeadless": true
}
```

### `POST /api/instances/viewport`

请求体：

```json
{
  "width": 1280,
  "height": 800
}
```

## 设置接口

### `GET /api/settings`

用途：读取全局程序设置。

关键响应字段：

- `searchUrlTemplate`
- `screencastFps`
- `pluginPanelWidth`
- `userDataDirectory`
- `userAgent`
- `allowInstanceUserAgentOverride`
- `headless`
- `settingsFilePath`
- `defaultUserDataDirectory`

### `POST /api/settings/autosave`

内容类型：`multipart/form-data`

主要表单字段：

- `SearchUrlTemplate`
- `ScreencastFps`
- `PluginPanelWidth`
- `UserDataDirectory`
- `UserAgent`
- `AllowInstanceUserAgentOverride`
- `Headless`

## 布局接口

### `POST /api/layout`

请求体：

```json
{
  "pluginPanelWidth": 320
}
```

## 日志接口

### `GET /api/logs/content`

用途：读取最近日志。

### `POST /api/logs/clear`

用途：清空日志文件。

## 截图与实时画面接口

### `GET /api/screenshot`

用途：获取当前活动页面截图，响应类型为 `image/png`。

### `POST /api/screencast/settings`

请求体：

```json
{
  "enabled": true,
  "maxWidth": 1280,
  "maxHeight": 800,
  "frameIntervalMs": 100
}
```

## 插件接口

### `GET /api/plugins`

用途：读取插件目录扫描结果。

### `POST /api/plugins/upload`

内容类型：`multipart/form-data`

说明：

- 字段名为 `files`
- 支持上传 DLL、ZIP，ZIP 会先解压再扫描 DLL

### `POST /api/plugins/load`

请求体：

```json
{
  "path": "C:/plugins/demo.dll"
}
```

### `POST /api/plugins/unload`

请求体：

```json
{
  "pluginId": "plugin-id"
}
```

### `POST /api/plugins/delete`

请求体：

```json
{
  "pluginId": "plugin-id"
}
```

### `POST /api/plugins/control`

请求体：

```json
{
  "pluginId": "plugin-id",
  "command": "start",
  "arguments": {
    "key": "value"
  }
}
```

### `POST /api/plugins/run`

请求体：

```json
{
  "pluginId": "plugin-id",
  "functionId": "function-id",
  "arguments": {
    "key": "value"
  }
}
```

## SignalR Hub

地址：`/browserHub`

### 服务端推送给客户端

- `ReceiveFrame(data, width, height)`
  - 推送 JPEG 帧数据
- `ScreencastDisabled()`
  - 通知前端实时画面已停用
- `ReceivePluginOutput(payload)`
  - 推送插件输出消息

### 客户端调用服务端

- `SendMouseEvent(MouseEventData data)`
- `SendKeyEvent(KeyEventData data)`

## 当前后端完成度说明

已完成：

- Electron 当前依赖的 REST API 已具备
- SignalR 输入与实时画面链路已具备
- 插件扫描、控制、执行、上传接口已具备
- 日志、设置、布局、截图接口已具备

当前已知兼容性约束：

- 实例设置接口已经返回 `UserAgent` 和更完整的 `Viewport` 结构，便于前端兼容旧界面
- 但按当前 Core 能力，实例设置真正持久化的重点仍是 `UserDataDirectory`、`Headless` 和基础视口尺寸
- 实例级 User-Agent 与更高级的视口策略，后续若要做到完全可编辑，需要继续扩 Core

这意味着：前端界面调整可以继续，但如果要把“实例级 UA 完整编辑能力”也作为目标，需要先补 Core 能力再改前端交互。