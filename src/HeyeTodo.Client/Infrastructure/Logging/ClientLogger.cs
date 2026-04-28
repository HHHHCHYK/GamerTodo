using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HeyeTodo.Client.Infrastructure;

namespace HeyeTodo.Client.Infrastructure.Logging;

public interface IClientLogger
{
    string LogFilePath { get; }

    Task LogAsync(ClientLogLevel level, string module, string message, IReadOnlyDictionary<string, object?>? properties = null, Exception? exception = null, CancellationToken ct = default);
    Task LogOperationAsync(string module, string operation, ClientLogLevel level, string message, IReadOnlyDictionary<string, object?>? properties = null, Exception? exception = null, CancellationToken ct = default);

    Task LogInfoAsync(string message, IReadOnlyDictionary<string, object?>? properties = null, CancellationToken ct = default);
    Task LogUserOperationExceptionAsync(string operation, Exception exception, IReadOnlyDictionary<string, object?>? properties = null, CancellationToken ct = default);
    Task LogSyncOperationAsync(string operation, ClientLogLevel level, string message, IReadOnlyDictionary<string, object?>? properties = null, Exception? exception = null, CancellationToken ct = default);
}

public enum ClientLogLevel
{
    Information,
    Warning,
    Error,
}

public sealed class FileClientLogger : IClientLogger
{
    private const int MaxLogFiles = 10;
    private const long MaxTotalLogBytes = 50L * 1024L * 1024L;

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ISettingsService _settings;

    public FileClientLogger(ISettingsService settings)
    {
        _settings = settings;
        Directory.CreateDirectory(AppPaths.LogDirectory);
        CleanupOldLogs();
        LogFilePath = AppPaths.CreateClientLogPath();
        File.WriteAllText(LogFilePath, string.Empty, Encoding.UTF8);
    }

    public string LogFilePath { get; }

    public Task LogAsync(ClientLogLevel level, string module, string message, IReadOnlyDictionary<string, object?>? properties = null, Exception? exception = null, CancellationToken ct = default)
        => WriteAsync(level, $"[{module}] ", message, properties, exception, ct);

    public Task LogOperationAsync(string module, string operation, ClientLogLevel level, string message, IReadOnlyDictionary<string, object?>? properties = null, Exception? exception = null, CancellationToken ct = default)
        => WriteAsync(level, $"[{module}] {operation}: ", message, properties, exception, ct);

    public Task LogInfoAsync(string message, IReadOnlyDictionary<string, object?>? properties = null, CancellationToken ct = default)
        => WriteAsync(ClientLogLevel.Information, "[App] ", message, properties, null, ct);

    public Task LogUserOperationExceptionAsync(string operation, Exception exception, IReadOnlyDictionary<string, object?>? properties = null, CancellationToken ct = default)
        => WriteAsync(ClientLogLevel.Error, $"[UserOperation] {operation}: ", exception.Message, properties, exception, ct);

    public Task LogSyncOperationAsync(string operation, ClientLogLevel level, string message, IReadOnlyDictionary<string, object?>? properties = null, Exception? exception = null, CancellationToken ct = default)
        => WriteAsync(level, $"[Sync] {operation}: ", message, properties, exception, ct);

    private async Task WriteAsync(ClientLogLevel level, string prefix, string message, IReadOnlyDictionary<string, object?>? properties, Exception? exception, CancellationToken ct)
    {
        try
        {
            if (!IsEnabled(level))
            {
                return;
            }

            var builder = new StringBuilder();
            builder.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            builder.Append(' ');
            builder.Append('【');
            builder.Append(ToDisplayLevel(level));
            builder.Append('】');
            builder.Append(prefix);
            builder.Append(message);

            var sanitized = properties is null ? null : Sanitize(properties);
            if (sanitized is { Count: > 0 })
            {
                builder.Append(" | ");
                builder.Append(string.Join(", ", sanitized.Select(x => $"{x.Key}={FormatValue(x.Value)}")));
            }

            if (exception is not null)
            {
                builder.Append(" | ");
                builder.Append(exception.GetType().FullName);
                builder.Append(": ");
                builder.Append(exception.Message);

                if (!string.IsNullOrWhiteSpace(exception.StackTrace))
                {
                    builder.AppendLine();
                    builder.Append(exception.StackTrace);
                }
            }

            builder.AppendLine();

            await _writeLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await File.AppendAllTextAsync(LogFilePath, builder.ToString(), Encoding.UTF8, ct).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch
        {
        }
    }

    private bool IsEnabled(ClientLogLevel level)
    {
        var configured = ParseLevel(_settings.Current.LogLevel);
        return level >= configured;
    }

    private static ClientLogLevel ParseLevel(string? value)
        => Enum.TryParse<ClientLogLevel>(value, ignoreCase: true, out var level) ? level : ClientLogLevel.Information;

    private static string ToDisplayLevel(ClientLogLevel level)
        => level switch
        {
            ClientLogLevel.Information => "Info",
            ClientLogLevel.Warning => "Warning",
            ClientLogLevel.Error => "Error",
            _ => level.ToString(),
        };

    private static string FormatValue(object? value)
        => value switch
        {
            null => "<null>",
            DateTimeOffset dateTime => dateTime.ToString("O"),
            DateTime dateTime => dateTime.ToString("O"),
            _ => value.ToString() ?? string.Empty,
        };

    private static IReadOnlyDictionary<string, object?> Sanitize(IReadOnlyDictionary<string, object?> properties)
        => properties
            .Where(x => !IsSensitiveKey(x.Key))
            .ToDictionary(x => x.Key, x => x.Value);

    private static bool IsSensitiveKey(string key)
    {
        var normalized = key.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        return normalized.Contains("token", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("password", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("key", StringComparison.OrdinalIgnoreCase);
    }

    private static void CleanupOldLogs()
    {
        try
        {
            var files = new DirectoryInfo(AppPaths.LogDirectory)
                .EnumerateFiles("client-*.log")
                .OrderByDescending(x => x.CreationTimeUtc)
                .ThenByDescending(x => x.LastWriteTimeUtc)
                .ToList();

            long totalBytes = 0;
            for (var i = 0; i < files.Count; i++)
            {
                totalBytes += files[i].Length;
                if (i >= MaxLogFiles || totalBytes > MaxTotalLogBytes)
                {
                    files[i].Delete();
                }
            }
        }
        catch
        {
        }
    }
}
