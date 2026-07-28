using System.Diagnostics;

namespace VoiceLab.Infrastructure;

public sealed class FileLogger
{
    private const long MaximumFileBytes = 2 * 1024 * 1024;
    private const int RetainedFileCount = 7;
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(10);
    private readonly string _dir;
    private readonly object _sync = new();
    private string? _lastErrorKey;
    private DateTimeOffset _lastErrorAt;

    public FileLogger(string? directory = null) => _dir = directory ?? VoiceLabStoragePaths.LogsDirectory;

    public void Log(string message, Exception? ex = null)
    {
        try
        {
            var now = DateTimeOffset.Now;
            var errorKey = ex is null ? null : $"{message}|{ex.GetType().FullName}|{ex.Message}";
            lock (_sync)
            {
                if (errorKey is not null && errorKey == _lastErrorKey && now - _lastErrorAt < DuplicateWindow) return;
                _lastErrorKey = errorKey;
                _lastErrorAt = now;
                Directory.CreateDirectory(_dir);
                var path = CurrentPath(now);
                RotateIfNeeded(path, now);
                var thread = Thread.CurrentThread;
                var threadLabel = string.IsNullOrWhiteSpace(thread.Name) ? $"managed:{thread.ManagedThreadId}" : $"{thread.Name} (managed:{thread.ManagedThreadId})";
                File.AppendAllText(path, $"{now:O} [thread {threadLabel}] {message}{(ex is null ? string.Empty : $"{Environment.NewLine}{ex}")}{Environment.NewLine}");
                PruneOldFiles();
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Conditional("DEBUG")]
    public void LogDiagnostic(string operation, TimeSpan elapsed) => Log($"Timing: {operation} completed in {elapsed.TotalMilliseconds:F1} ms");

    private string CurrentPath(DateTimeOffset now) => Path.Combine(_dir, now.UtcDateTime.ToString("yyyy-MM-dd") + ".log");
    private void RotateIfNeeded(string path, DateTimeOffset now)
    {
        if (!File.Exists(path) || new FileInfo(path).Length < MaximumFileBytes) return;
        File.Move(path, Path.Combine(_dir, $"{Path.GetFileNameWithoutExtension(path)}-{now:HHmmssfff}.log"));
    }
    private void PruneOldFiles()
    {
        foreach (var file in new DirectoryInfo(_dir).EnumerateFiles("*.log").OrderByDescending(file => file.LastWriteTimeUtc).Skip(RetainedFileCount))
            try { file.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
