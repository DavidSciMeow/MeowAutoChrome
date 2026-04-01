using MeowAutoChrome.Core.Models;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.WebAPI.Services;

/// <summary>
/// 将 Microsoft.Extensions.Logging 日志写入 AppLogService 的日志提供器。<br/>
/// Logger provider that writes Microsoft.Extensions.Logging output into AppLogService.
/// </summary>
public sealed class AppLogLoggerProvider(Core.Services.AppLogService appLogService) : ILoggerProvider
{
    /// <summary>
    /// 创建指定分类的日志记录器。<br/>
    /// Create a logger for the specified category.
    /// </summary>
    /// <param name="categoryName">日志分类名。<br/>Log category name.</param>
    /// <returns>日志记录器实例。<br/>Logger instance.</returns>
    public ILogger CreateLogger(string categoryName) => new AppLogLogger(categoryName, appLogService);

    /// <summary>
    /// 释放日志提供器。<br/>
    /// Dispose the logger provider.
    /// </summary>
    public void Dispose() { }

    private sealed class AppLogLogger(string categoryName, Core.Services.AppLogService appLogService) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

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
