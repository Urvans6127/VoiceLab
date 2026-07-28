using System.Text.Json;

namespace VoiceLab.Infrastructure;

public sealed class JsonSettingsStore : ISettingsStore
{
    private const long MaxSettingsBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _path;
    public string Path => _path;

    public JsonSettingsStore(string? path = null) => _path = path ?? VoiceLabStoragePaths.SettingsFile;

    public async Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return new ApplicationSettings();
        try
        {
            if (new FileInfo(_path).Length > MaxSettingsBytes)
            {
                Quarantine();
                return new ApplicationSettings();
            }
            ApplicationSettings? settings;
            await using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                settings = await JsonSerializer.DeserializeAsync<ApplicationSettings>(stream, Options, cancellationToken).ConfigureAwait(false);
            if (settings is null || settings.SchemaVersion is < 1 or > ApplicationSettings.CurrentSchemaVersion) throw new JsonException("Unsupported settings schema.");
            var migrated = settings.Validate();
            if (settings.SchemaVersion < ApplicationSettings.CurrentSchemaVersion) try { await SaveAsync(migrated, cancellationToken).ConfigureAwait(false); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return migrated;
        }
        catch (JsonException) { Quarantine(); return new ApplicationSettings(); }
        catch (NotSupportedException) { Quarantine(); return new ApplicationSettings(); }
        catch (IOException) { return new ApplicationSettings(); }
        catch (UnauthorizedAccessException) { return new ApplicationSettings(); }
    }

    public async Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = System.IO.Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, settings.Validate(), Options, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }
            File.Move(temp, _path, true);
        }
        finally { if (File.Exists(temp)) try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    private void Quarantine()
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(_path)!;
            var quarantine = System.IO.Path.Combine(directory, $"settings.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
            File.Move(_path, quarantine, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
