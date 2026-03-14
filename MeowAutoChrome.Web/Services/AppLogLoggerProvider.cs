using MeowAutoChrome.Web.Models;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Web.Services;

public sealed class AppLogLoggerProvider(AppLogService appLogService) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
        => new AppLogLogger(categoryName, appLogService);

    public void Dispose()
    {
    }

    private sealed class AppLogLogger(string categoryName, AppLogService appLogService) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (exception is not null)
                message = string.IsNullOrWhiteSpace(message)
                    ? exception.ToString()
                    : $"{message}{Environment.NewLine}{exception}";

            appLogService.WriteEntry(new AppLogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = logLevel,
                Category = categoryName,
                Message = message
            });
        }
    }
}
