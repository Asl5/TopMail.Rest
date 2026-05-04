using System.Globalization;
using Microsoft.Extensions.Logging;
using TopMail.Rest.Options;

namespace TopMail.Rest.Services;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLoggingOptions _options;
    private readonly object _writeLock = new();

    public FileLoggerProvider(FileLoggingOptions options)
    {
        _options = options;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, this);
    }

    public void Dispose()
    {
    }

    internal bool IsEnabled(LogLevel logLevel)
    {
        return _options.Enabled
               && logLevel != LogLevel.None
               && logLevel >= _options.MinimumLevel;
    }

    internal void Write(
        LogLevel logLevel,
        string categoryName,
        EventId eventId,
        string message,
        Exception? exception)
    {
        if (!IsEnabled(logLevel))
            return;

        try
        {
            var directoryPath = string.IsNullOrWhiteSpace(_options.DirectoryPath)
                ? Path.Combine(AppContext.BaseDirectory, "logs")
                : _options.DirectoryPath;
            Directory.CreateDirectory(directoryPath);

            var fileNamePrefix = SanitizeFileNamePart(_options.FileNamePrefix, "topmail");
            var dateFormat = string.IsNullOrWhiteSpace(_options.FileNameDateFormat)
                ? "yyyyMMdd"
                : _options.FileNameDateFormat;
            var datePart = DateTime.Now.ToString(dateFormat, CultureInfo.InvariantCulture);
            var filePath = Path.Combine(directoryPath, $"{fileNamePrefix}-{datePart}.log");

            var eventSuffix = eventId.Id != 0 || !string.IsNullOrWhiteSpace(eventId.Name)
                ? $" ({eventId.Id}:{eventId.Name ?? "-"})"
                : string.Empty;
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{ToShortLevel(logLevel)}] {categoryName}{eventSuffix}: {message}";
            if (exception is not null)
            {
                line += $"{Environment.NewLine}{exception}";
            }

            lock (_writeLock)
            {
                File.AppendAllText(filePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Never fail business flow due to logging errors.
        }
    }

    private static string SanitizeFileNamePart(string? value, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            candidate = candidate.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(candidate) ? fallback : candidate;
    }

    private static string ToShortLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "UNK"
        };
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly FileLoggerProvider _provider;

        public FileLogger(string categoryName, FileLoggerProvider provider)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _provider.IsEnabled(logLevel);
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            _provider.Write(logLevel, _categoryName, eventId, message, exception);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
