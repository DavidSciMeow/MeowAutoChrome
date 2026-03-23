using MeowAutoChrome.Core.Models;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Web.Services;

/// <summary>
/// 将日志写入应用内日志存储的 ILoggerProvider 实现，用于将 Microsoft.Extensions.Logging 的日志重定向到 AppLogService。
/// </summary>
public sealed class AppLogLoggerProvider(Core.Services.AppLogService appLogService) : ILoggerProvider
{

    /// <summary>
    /// 创建一个新的 AppLogLogger 实例。
    /// </summary>
    /// <param name="categoryName">日志分类名称。</param>
    /// <returns>ILogger 实例。</returns>
    public ILogger CreateLogger(string categoryName) => new AppLogLogger(categoryName, appLogService);

    /// <summary>
    /// 释放资源（无实际操作）。
    /// </summary>
    public void Dispose() { }

    /// <summary>
    /// appLogService 的 ILogger 实现，将日志写入 AppLogService。
    /// </summary>
    /// <param name="categoryName">日志分类名称。</param>
    /// <param name="appLogService">AppLogService 实例。</param>
    private sealed class AppLogLogger(string categoryName, Core.Services.AppLogService appLogService) : ILogger
    {
        /// <summary>
        /// 开始一个日志作用域（不支持，始终返回 null）。
        /// </summary>
        /// <typeparam name="TState">状态对象类型。</typeparam>
        /// <param name="state">状态对象。</param>
        /// <returns>始终为 null。</returns>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <summary>
        /// 判断指定日志级别是否启用。
        /// </summary>
        /// <param name="logLevel">日志级别。</param>
        /// <returns>是否启用。</returns>
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        /// <summary>
        /// 将日志写入 AppLogService。
        /// </summary>
        /// <typeparam name="TState">日志状态对象类型。</typeparam>
        /// <param name="logLevel">日志级别。</param>
        /// <param name="eventId">事件 ID。</param>
        /// <param name="state">日志状态对象。</param>
        /// <param name="exception">异常信息。</param>
        /// <param name="formatter">日志格式化委托。</param>
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
