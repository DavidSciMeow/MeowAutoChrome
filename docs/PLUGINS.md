# 插件开发与加载指南

本章为插件作者提供详尽的参考：如何声明插件、定义动作与输入、参数如何绑定、返回值如何规范化、以及运行时可调用的宿主能力和打包注意事项。

核心约定与位置

- 插件契约（接口/基类/attribute）位于 `MeowAutoChrome.Contracts`，关键文件：
  - `IPlugin.cs`, `PluginBase.cs`, `PluginPrimitives.cs`
  - 属性定义在 `MeowAutoChrome.Contracts/Attributes/`（`PluginAttribute`, `PActionAttribute`, `PInputAttribute`, `PluginExportAttribute`）
- 插件发现在 Core：`MeowAutoChrome.Core/Services/PluginDiscovery`（类型检测、参数元数据生成）
- 插件加载/执行在 Core 的 PluginHost 实现：`MeowAutoChrome.Core/Services/PluginHost`（`BrowserPluginHostCore`, `PluginExecutionService`, `PluginExecutor`, `PluginLoadContext` 等）

一、如何声明插件（类型级别）

- 使用 `PluginAttribute` 将类标识为插件：

```csharp
using MeowAutoChrome.Contracts.Attributes;

[Plugin("com.example.helloworld", "Hello World", "示例插件")] 
public class HelloWorldPlugin : PluginBase
{
        // 实现生命周期方法或动作
}
```

二、动作（Action）声明 — `PActionAttribute`

- 使用 `PActionAttribute` 标注方法以将其暴露为插件动作。属性可选字段：`Id`, `Name`, `Description`。
- 若未提供 `Id`，系统会使用方法名作为 action id，并在必要时自动去重（附加后缀）。

示例（多种返回形态）：

```csharp
using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts.Attributes;

[Plugin("com.example.hello","Hello Plugin")]
public class HelloPlugin : PluginBase
{
        [PAction(Name = "Say Hello", Description = "返回问候文本")]
        public IResult SayHello(string name) => Result.Ok(new { Text = $"Hello {name}" });

        [PAction]
        public async Task<IResult> DoAsyncWork(int times)
        {
                for (int i = 0; i < times; i++) await Task.Delay(10);
                return Result.Ok(new { Count = times });
        }

        [PAction(Id = "get_title")]
        public async Task<string> GetTitle() // 返回 Task<T> 会被宿主包装为 Result.Data
        {
                await Task.Delay(1);
                return "title";
        }
}
```

三、参数声明与 `PInputAttribute`

- `PInputAttribute` 可用于方法参数或属性上以提供 UI 元数据：
  - `Name`：参数键名（通常等于方法参数名）
  - `Label`：显示标签
  - `Description`：说明
  - `DefaultValue`：默认值（字符串形式）
  - `Required`：是否必填
  - `InputType`：类型提示（例如 `text`, `password`, `select`, `textarea`）
  - `Multiline`、`Rows`：用于 textarea

示例：

```csharp
[PAction]
public IResult Login(
        [PInput(Label="用户名", Required=true)] string username,
        [PInput(Label="密码", InputType="password")] string password)
{
        // ...
}
```

四、参数绑定规则（运行时）

- 调用时传递的参数为字符串键值对（`Dictionary<string,string?>`），主机通过参数名与方法参数匹配：
  - 参数键使用方法参数名（`parameter.Name`）。
  - 若方法参数类型可分配为 `IPluginContext`，宿主会注入当前 `IPluginContext` 实例（无需在调用参数中提供）。
- 类型转换支持（字符串 -> 目标类型）：
  - `string`：原值
  - `Guid`：`Guid.Parse`
  - `DateTime` / `DateTimeOffset`：ISO 风格解析（CultureInvariant）
  - `enum`：按名称解析（忽略大小写）
  - 数值类型：使用 `Convert.ChangeType`
  - 带 ? 的可空类型：当字符串为空/空白时会被转换为 `null`
  - 若未提供参数且方法参数有默认值，则使用默认值；否则：值类型使用默认构造，引用类型为 `null`。

（实现细节位于 `MeowAutoChrome.Core/Services/PluginDiscovery/PluginParameterBinder.cs`）

五、返回值规范化（宿主如何处理方法返回）

- 宿主接受任意返回形态并进行规范化处理（见 `PluginExecutionService`）：
  - `Task<IResult>`：等待并返回其 IResult
  - `IResult`：直接使用
  - `Task`（无结果）：等待完成，返回成功 `Result.Ok()`
  - `Task<T>`：等待完成后，若 `T` 实现 `IResult` 则直接使用，否则将 `T` 包装为 `Result.Data`
  - 同步返回 `T`：若为 `IResult` 则直接使用，否则包装为 `Result`（Data = 返回对象）

六、生命周期方法与宿主注入

- 插件应实现或继承 `PluginBase`，并实现生命周期方法：
  - `StartAsync()`, `StopAsync()`, `PauseAsync()`, `ResumeAsync()`（均返回 `Task<IResult>`）
  - `SupportsPause` 属性控制宿主是否允许 pause/resume
- 在动作或生命周期方法执行期间，宿主会将 `HostContext` 注入到插件实例（`PluginBase.HostContext` 可访问），并在执行结束后清理。

七、宿主能力（通过 `IPluginContext` / `IPluginHost` 暴露）

- 常见可用能力（`PluginBase` 提供便捷封装）：
  - 日志写入：`Host.WriteLogAsync(...)` 或 `WriteLogAsync(...)`
  - 发布运行中消息/Toast：`PublishUpdateAsync` / `ToastAsync`（`PluginBase.MessageAsync` / `ToastAsync` 封装）
  - 浏览器实例管理：`CreateBrowserInstanceAsync`, `CloseBrowserInstanceAsync`, `SelectBrowserInstanceAsync`（`BrowserCreationOptions` 可指定 `BrowserType`, `Headless`, `UserDataDirectory` 等字段）
  - 访问当前页面/BrowserContext/Browser：`Context.ActivePage`, `Context.BrowserContext`, `Context.Browser`

八、如何从外部调用插件（WebAPI 示例）

- 控制命令（start/stop/pause/resume）：

POST /api/plugins/control

Body JSON:

```json
{
    "pluginId":"com.example.hello",
    "command":"start",
    "arguments": { "someParam": "value" }
}
```

- 调用动作（函数）示例：

POST /api/plugins/run

Body JSON:

```json
{
    "pluginId":"com.example.hello",
    "functionId":"SayHello",
    "arguments": { "name": "Alice", "times": "3" }
}
```

注意：`arguments` 中所有值均为字符串（宿主负责转换到目标类型）。

九、发现与元数据（UI 友好）

- 主机会扫描插件程序集并通过 `PActionAttribute` 与 `PInputAttribute` 收集动作列表及参数元数据，供前端展示表单（包括 `InputType`, `Options` 列表、默认值、是否必填等）。
  - 枚举参数会被展示为 `select`，选项由枚举成员名生成。

十、插件加载与隔离（重要）

- 插件以可回收的 `AssemblyLoadContext` 加载（见 `PluginLoadContext`），但会把若干程序集视为共享并重用主机已加载的副本：
  - `MeowAutoChrome.Contracts` 与 `Microsoft.Playwright`（以及 `Microsoft.Bcl.AsyncInterfaces`）被列为共享程序集。
- 因此：**不要**在插件发布包中包含 `MeowAutoChrome.Contracts.dll` 或 `Microsoft.Playwright.*` 的私有副本；否则会导致类型不兼容而被主机跳过。

十一、包装与发布建议

- 将插件作为单独的 Class Library 项目（`.csproj`）发布为 DLL，只包含插件自己的业务代码和资源。
- 在开发时引用 `MeowAutoChrome.Contracts` 源或包（保持版本一致）。
- 测试插件：将编译产物放入插件目录或通过 `POST /api/plugins/upload` 上传并调用 `/api/plugins/refresh` 或相关端点检查发现。

十二、调试建议与常见问题

- 插件未被发现或被跳过：确认 Contracts 版本一致、检查插件加载日志与 `PluginDiscoverySnapshot`。
- 参数类型转换失败：确认传入参数为字符串且可转换为目标类型（例如枚举名称/数值、ISO 时间字符串等）。
- 无法创建浏览器实例：查看 `BrowserCreationOptions` 字段（`UserDataDirectory` 的目录权限/路径限制、被禁止的 args、最大实例配额等）。

附录：简短示例（完整）

```csharp
using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts.Attributes;

[Plugin("com.example.calc", "Calculator Plugin", "示例：接受参数并返回结果")]
public class CalcPlugin : PluginBase
{
        [PAction(Name = "Add Numbers")]
        public IResult Add(
                [PInput(Label = "被加数", Required = true)] int a,
                [PInput(Label = "加数", Required = true)] int b)
        {
                return Result.Ok(new { Sum = a + b });
        }

        [PAction]
        public async Task<string> Echo([PInput(Label = "文本", Multiline = true, Rows = 4)] string text)
        {
                await Task.Delay(10);
                return text;
        }
}
```

参考示例工程：`MeowAutoChrome.ExamplePlugin`。

—— 结束 ——
