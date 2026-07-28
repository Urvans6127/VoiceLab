using VoiceLab.Infrastructure;
using System.IO;

namespace VoiceLab.App.Services;

public sealed class SettingsCoordinator(ISettingsStore store) : IDisposable
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private CancellationTokenSource? _debounce;
    public ApplicationSettings Current { get; private set; } = new();
    public event Action<ApplicationSettings>? SettingsChanged;

    public async Task InitializeAsync() => Current = await store.LoadAsync().ConfigureAwait(false);

    public void Update(Func<ApplicationSettings, ApplicationSettings> update)
    {
        lock (_sync)
        {
            Current = update(Current).Validate();
            SettingsChanged?.Invoke(Current);
            _debounce?.Cancel();
            _debounce?.Dispose();
            _debounce = new CancellationTokenSource();
            _ = SaveAfterDelayAsync(_debounce.Token);
        }
    }

    public async Task FlushAsync()
    {
        CancellationTokenSource? pending;
        lock (_sync) { pending = _debounce; _debounce = null; }
        pending?.Cancel();
        pending?.Dispose();
        await SaveCurrentAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task SaveAfterDelayAsync(CancellationToken token)
    {
        try { await Task.Delay(600, token).ConfigureAwait(false); await SaveCurrentAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private async Task SaveCurrentAsync(CancellationToken token)
    {
        await _saveGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ApplicationSettings snapshot;
            lock(_sync)snapshot=Current;
            await store.SaveAsync(snapshot,token).ConfigureAwait(false);
        }
        finally{_saveGate.Release();}
    }

    public void Dispose() { _debounce?.Cancel(); _debounce?.Dispose(); }
}
