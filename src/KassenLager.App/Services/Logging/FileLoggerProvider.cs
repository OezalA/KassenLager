using Microsoft.Extensions.Logging;

namespace KassenLager.App.Services.Logging;

/// <summary>
/// Minimal daily-rolling file logger. Technical details go here; users only see
/// friendly messages. Files stay on the device (app data folder).
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const int RetentionDays = 30;

    private readonly string _directory;
    private readonly Lock _writeLock = new();

    public FileLoggerProvider(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
        DeleteOldFiles();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Write(LogLevel level, string category, string message, Exception? exception)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {category}: {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        var path = Path.Combine(_directory, $"kassenlager-{DateTime.Now:yyyyMMdd}.log");
        lock (_writeLock)
        {
            try
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (IOException)
            {
                // Logging must never crash the app.
            }
        }
    }

    private void DeleteOldFiles()
    {
        var cutoff = DateTime.Now.AddDays(-RetentionDays);
        foreach (var file in Directory.EnumerateFiles(_directory, "kassenlager-*.log"))
        {
            if (File.GetLastWriteTime(file) < cutoff)
            {
                File.Delete(file);
            }
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                provider.Write(logLevel, category, formatter(state, exception), exception);
            }
        }
    }
}
