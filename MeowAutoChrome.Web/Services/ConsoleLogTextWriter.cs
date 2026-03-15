using System.Text;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Web.Services;

/// <summary>
/// 将控制台输出捕获并写入 AppLogService 的 TextWriter 实现，通常用于将控制台日志也写入应用日志文件。
/// 它会在行结束时将缓冲的一行写入 AppLogService。
/// </summary>
public sealed class ConsoleLogTextWriter(TextWriter innerWriter, AppLogService appLogService, LogLevel logLevel, string category) : TextWriter
{
    private readonly object _syncRoot = new();
    private readonly StringBuilder _lineBuffer = new();

    public override Encoding Encoding => innerWriter.Encoding;

    public override void Write(char value)
    {
        innerWriter.Write(value);
        AppendText(value.ToString());
    }

    public override void Write(char[] buffer, int index, int count)
    {
        innerWriter.Write(buffer, index, count);
        AppendText(new string(buffer, index, count));
    }

    public override void Write(string? value)
    {
        innerWriter.Write(value);
        AppendText(value);
    }

    public override void WriteLine()
    {
        innerWriter.WriteLine();
        FlushPendingLine();
    }

    public override void WriteLine(string? value)
    {
        innerWriter.WriteLine(value);
        AppendText(value);
        FlushPendingLine();
    }

    public override void Flush()
    {
        innerWriter.Flush();
        FlushPendingLine();
    }

    private void AppendText(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        lock (_syncRoot)
        {
            foreach (var character in value)
            {
                if (character == '\r')
                    continue;

                if (character == '\n')
                {
                    FlushPendingLineCore();
                    continue;
                }

                _lineBuffer.Append(character);
            }
        }
    }

    private void FlushPendingLine()
    {
        lock (_syncRoot)
            FlushPendingLineCore();
    }

    private void FlushPendingLineCore()
    {
        if (_lineBuffer.Length == 0)
            return;

        appLogService.WriteEntry(logLevel, _lineBuffer.ToString(), category);
        _lineBuffer.Clear();
    }
}
