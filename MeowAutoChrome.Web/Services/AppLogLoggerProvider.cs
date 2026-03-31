using MeowAutoChrome.Core.Models;

namespace MeowAutoChrome.Web.Services;

/// <summary>
/// 将日志写入应用内日志存储的 ILoggerProvider 实现，用于将 Microsoft.Extensions.Logging 的日志重定向到 AppLogService。<br/>
/// ILoggerProvider implementation that writes logs into the in-app log store, redirecting Microsoft.Extensions.Logging to AppLogService.
/// </summary>
public sealed class AppLogLoggerProvider(Core.Services.AppLogService appLogService) : ILoggerProvider
{

    /// <summary>
    /// 创建一个新的 AppLogLogger 实例。<br/>
    /// Create a new AppLogLogger instance.
    /// </summary>
    /// <param name="categoryName">日志分类名称 / category name.</param>
    /// <returns>ILogger 实例 / ILogger instance.</returns>
    public ILogger CreateLogger(string categoryName) => new AppLogLogger(categoryName, appLogService);

    /// <summary>
    /// 释放资源（无实际操作）。<br/>
    /// Dispose resources (no-op in this implementation).
    /// </summary>
    public void Dispose() { }

    /// <summary>
    /// appLogService 的 ILogger 实现，将日志写入 AppLogService。<br/>
    /// ILogger implementation that writes logs into AppLogService.
    /// </summary>
    /// <param name="categoryName">日志分类名称 / category name.</param>
    /// <param name="appLogService">AppLogService 实例 / AppLogService instance.</param>
    private sealed class AppLogLogger(string categoryName, Core.Services.AppLogService appLogService) : ILogger
    {
        /// <summary>
        /// 开始一个日志作用域（不支持，始终返回 null）。<br/>
        /// Begin a log scope (not supported, always returns null).
        /// </summary>
        /// <typeparam name="TState">状态对象类型 / type of the state object.</typeparam>
        /// <param name="state">状态对象 / state object.</param>
        /// <returns>始终为 null / always null.</returns>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <summary>
        /// 判断指定日志级别是否启用。<br/>
        /// Determine whether the specified log level is enabled.
        /// </summary>
        /// <param name="logLevel">日志级别 / log level.</param>
        /// <returns>是否启用 / whether enabled.</returns>
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        /// <summary>
        /// 将日志写入 AppLogService。<br/>
        /// Write a log entry to AppLogService.
        /// </summary>
        /// <typeparam name="TState">日志状态对象类型 / type of the log state object.</typeparam>
        /// <param name="logLevel">日志级别 / log level.</param>
        /// <param name="eventId">事件 ID / event id.</param>
        /// <param name="state">日志状态对象 / log state object.</param>
        /// <param name="exception">异常信息 / exception, if any.</param>
        /// <param name="formatter">日志格式化委托 / formatter delegate.</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (exception is not null)
                message = string.IsNullOrWhiteSpace(message)
                    ? exception.ToString()
                    : $"{message}{Environment.NewLine}{exception}";

            appLogService.WriteEntry(new AppLogEntry(DateTimeOffset.Now, logLevel, categoryName, message));
        }
    }
}
