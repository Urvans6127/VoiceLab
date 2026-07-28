using VoiceLab.App.Services;
using VoiceLab.Infrastructure;

namespace VoiceLab.Tests;

public sealed class SettingsCoordinatorTests
{
    [Fact]
    public async Task FlushSerializesWithPendingSaveAndPersistsNewestSnapshot()
    {
        var store=new BlockingSettingsStore();
        using var coordinator=new SettingsCoordinator(store);
        await coordinator.InitializeAsync();
        coordinator.Update(settings=>settings with{WindowWidth=1000});
        await store.FirstSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        coordinator.Update(settings=>settings with{WindowWidth=1200});
        var flush=coordinator.FlushAsync();
        store.ReleaseFirstSave.SetResult();
        await flush;
        Assert.Equal(1,store.MaximumConcurrency);
        Assert.Equal(1200,store.Saved.Last().WindowWidth);
    }

    private sealed class BlockingSettingsStore:ISettingsStore
    {
        private int _active;
        public TaskCompletionSource FirstSaveStarted{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstSave{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<ApplicationSettings> Saved{get;}=[];
        public int MaximumConcurrency{get;private set;}
        public string Path=>"memory";
        public Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken=default)=>Task.FromResult(new ApplicationSettings());
        public async Task SaveAsync(ApplicationSettings settings,CancellationToken cancellationToken=default)
        {
            var active=Interlocked.Increment(ref _active);MaximumConcurrency=Math.Max(MaximumConcurrency,active);
            try
            {
                if(Saved.Count==0){FirstSaveStarted.TrySetResult();await ReleaseFirstSave.Task.WaitAsync(cancellationToken);}
                Saved.Add(settings);
            }
            finally{Interlocked.Decrement(ref _active);}
        }
    }
}
