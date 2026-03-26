using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts;

/// <summary>
/// 宿主上下文接口，提供插件运行时所需的环境信息和操作接口，包括浏览器上下文、活动页面、浏览器实例ID、浏览器实例管理器、插件启动参数、插件ID、目标ID、取消令牌以及向宿主发送更新的功能，插件可以通过这个接口获取运行时所需的信息和能力，并与宿主进行交互，如发布更新消息等
/// </summary>
public interface IHostContext
{
    /// <summary>
    /// 浏览器上下文，提供对浏览器环境的访问和操作能力，插件可以通过这个属性获取当前浏览器实例的上下文，以便在该实例中执行浏览器相关的操作或获取相关的信息
    /// </summary>
    IBrowserContext BrowserContext { get; }
    /// <summary>
    /// 活动页面，可能为null，如果插件需要操作页面但未提供活动页面，则应抛出异常，插件可以通过这个属性获取当前浏览器实例的活动页面，以便在该页面中执行浏览器相关的操作或获取相关的信息，如果没有活动页面，则返回null
    /// </summary>
    IPage? ActivePage { get; }
    /// <summary>
    /// 浏览器实例ID，唯一标识当前插件所在的浏览器实例，插件可以通过这个ID与宿主进行交互或区分不同的浏览器实例，插件可以通过这个属性获取当前浏览器实例的ID，以便在与宿主进行交互时使用该ID进行区分或识别，如果没有提供浏览器实例ID，则抛出异常
    /// </summary>
    string BrowserInstanceId { get; }
    /// <summary>
    /// 浏览器实例管理器，提供对浏览器实例的管理和操作能力，插件可以通过这个接口获取其他浏览器实例的信息或执行跨实例的操作，插件可以通过这个属性获取浏览器实例管理器，以便在需要获取其他浏览器实例的信息或执行跨实例的操作时使用该管理器进行相关的操作，如果没有提供浏览器实例管理器，则抛出异常
    /// </summary>
    IBrowserInstanceManager BrowserInstanceManager { get; }
    /// <summary>
    /// 浏览器参数，包含插件启动时传递的键值对参数，插件可以通过这些参数获取运行时所需的信息或配置，如果没有参数则为一个空字典，插件可以通过这个属性获取插件启动时传递的参数，以便在运行时使用这些参数进行相关的操作或配置，如果没有提供参数，则返回一个空字典
    /// </summary>
    IReadOnlyDictionary<string, string?> Arguments { get; }
    /// <summary>
    /// 插件ID，唯一标识当前插件，插件可以通过这个ID与宿主进行交互或区分不同的插件实例，插件可以通过这个属性获取当前插件的ID，以便在与宿主进行交互时使用该ID进行区分或识别，如果没有提供插件ID，则抛出异常
    /// </summary>
    string PluginId { get; }
    /// <summary>
    /// 目标ID，表示当前插件的目标对象的ID，插件可以通过这个属性获取当前插件的目标ID，以便在与宿主进行交互时使用该ID进行区分或识别，如果没有提供目标ID，则返回null，插件可以根据实际需求使用目标ID来标识不同的目标对象，如页面、元素等，以便在与宿主进行交互时进行相关的操作或展示，如果没有提供目标ID，则表示没有特定的目标对象，插件可以根据实际需求进行相应的处理
    /// </summary>
    string TargetId { get; }
    /// <summary>
    /// 取消令牌，用于在插件执行过程中支持取消操作，插件可以通过这个属性获取取消令牌，以便在需要支持取消操作的场景中使用该令牌进行相关的操作，如取消正在执行的任务或操作，如果没有提供取消令牌，则返回一个默认的不可取消的令牌
    /// </summary>
    CancellationToken CancellationToken { get; }
    /// <summary>
    /// 发送更新消息给宿主，参数包括消息、数据和是否打开模态框，插件可以通过这个方法向宿主发送更新消息，以便在宿主中进行相关的操作或展示
    /// </summary>
    /// <param name="message">要发送的消息内容，可以为null</param>
    /// <param name="data">要发送的附加数据，是一个键值对字典，可以为null</param>
    /// <param name="openModal">是否在宿主中打开模态框来展示该消息，默认为true，如果为false，则表示不打开模态框，而是以其他方式展示该消息，如通知栏等</param>
    /// <returns></returns>
    Task PublishUpdateAsync(string? message, IReadOnlyDictionary<string, string?>? data = null, bool openModal = true);
}
