namespace MeowAutoChrome.Core.Services;

/// <summary>
/// 将控制台输出捕获并写入 AppLogService 的 TextWriter 实现。<br/>
/// TextWriter implementation that captures console output and forwards lines to AppLogService.
/// 它会在行结束时将缓冲的一行写入 AppLogService。<br/>
/// It flushes buffered line content to AppLogService when a newline is encountered.
/// </summary>
/// <param name="innerWriter">底层要写入的 TextWriter / inner writer to forward console output to.</param>
/// <param name="appLogService">用于写入行日志的 AppLogService 实例 / AppLogService instance used to write lines.</param>
/// <param name="logLevel">用于写入日志的默认 LogLevel / default LogLevel to use for written entries.</param>
/// <param name="category">用于日志的类别字符串 / category string to use for log entries.</param>
public sealed class ConsoleLogTextWriter(TextWriter innerWriter, AppLogService appLogService, LogLevel logLevel, string category) : TextWriter
{
    private readonly Lock _syncRoot = new();
    private readonly StringBuilder _lineBuffer = new();
    /// <summary>
    /// 获取底层写入器使用的编码。<br/>
    /// Gets the encoding used by the inner writer.
    /// </summary>
    public override Encoding Encoding => innerWriter.Encoding;
    /// <summary>
    /// 写入单个字符到内部写入器并缓存到行缓冲。<br/>
    /// Writes a single character to the inner writer and buffers it for line logging.
    /// </summary>
    /// <param name="value">要写入的字符 / character to write.</param>
    public override void Write(char value)
    {
        innerWriter.Write(value);
        AppendText(value.ToString());
    }
    /// <summary>
    /// 将字符数组的一段写入内部写入器并缓存到行缓冲。<br/>
    /// Writes a range of a character array to the inner writer and buffers it for line logging.
    /// </summary>
    /// <param name="buffer">字符数组 / character buffer.</param>
    /// <param name="index">起始索引 / start index.</param>
    /// <param name="count">要写入的字符数 / number of characters to write.</param>
    public override void Write(char[] buffer, int index, int count)
    {
        innerWriter.Write(buffer, index, count);
        AppendText(new string(buffer, index, count));
    }
    /// <summary>
    /// 将字符串写入内部写入器并缓存到行缓冲。<br/>
    /// Writes a string to the inner writer and buffers it for line logging.
    /// </summary>
    /// <param name="value">要写入的字符串 / string to write.</param>
    public override void Write(string? value)
    {
        innerWriter.Write(value);
        AppendText(value);
    }
    /// <summary>
    /// 写入换行并刷新待处理的行到日志服务。<br/>
    /// Writes a line terminator to the inner writer and flushes any pending buffered line to the log service.
    /// </summary>
    public override void WriteLine()
    {
        innerWriter.WriteLine();
        FlushPendingLine();
    }
    /// <summary>
    /// 将字符串写为一行到内部写入器并将缓冲行发送到日志服务。<br/>
    /// Writes a string followed by a line terminator to the inner writer and flushes the buffered line to the log service.
    /// </summary>
    /// <param name="value">要写入的字符串 / string to write as a line.</param>
    public override void WriteLine(string? value)
    {
        innerWriter.WriteLine(value);
        AppendText(value);
        FlushPendingLine();
    }
    /// <summary>
    /// 刷新内部写入器并将任何待处理的行发送到日志服务。<br/>
    /// Flushes the inner writer and sends any pending buffered line to the log service.
    /// </summary>
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
