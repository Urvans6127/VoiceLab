namespace VoiceLab.Audio;

public sealed class EngineLifecycleCoordinator
{
    private readonly SemaphoreSlim _gate=new(1,1);private CancellationTokenSource? _startupCancellation;private int _state=(int)AudioEngineState.Stopped;
    public AudioEngineState State=>(AudioEngineState)Volatile.Read(ref _state);
    public event Action<AudioEngineState,Exception?>? StateChanged;

    public async Task<bool> StartAsync(Func<CancellationToken,Task> start,Func<Task> cleanup,CancellationToken cancellationToken=default)
    {
        if(State is AudioEngineState.Starting or AudioEngineState.Running)return false;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if(State is AudioEngineState.Starting or AudioEngineState.Running)return false;
            _startupCancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);Set(AudioEngineState.Starting);
            try{await start(_startupCancellation.Token).ConfigureAwait(false);_startupCancellation.Token.ThrowIfCancellationRequested();Set(AudioEngineState.Running);return true;}
            catch(OperationCanceledException){await cleanup().ConfigureAwait(false);Set(AudioEngineState.Stopped);return false;}
            catch(Exception ex){await cleanup().ConfigureAwait(false);Set(AudioEngineState.Faulted,ex);throw;}
            finally{_startupCancellation.Dispose();_startupCancellation=null;}
        }
        finally{_gate.Release();}
    }

    public async Task StopAsync(Func<Task> cleanup,CancellationToken cancellationToken=default)
    {
        _startupCancellation?.Cancel();await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try{if(State==AudioEngineState.Stopped)return;Set(AudioEngineState.Stopping);try{await cleanup().ConfigureAwait(false);}finally{Set(AudioEngineState.Stopped);}}
        finally{_gate.Release();}
    }

    public async Task FaultAsync(Exception error,Func<Task> cleanup)
    {
        await _gate.WaitAsync().ConfigureAwait(false);try{if(State is AudioEngineState.Stopped or AudioEngineState.Stopping)return;await cleanup().ConfigureAwait(false);Set(AudioEngineState.Faulted,error);}finally{_gate.Release();}
    }
    private void Set(AudioEngineState state,Exception? error=null){Volatile.Write(ref _state,(int)state);StateChanged?.Invoke(state,error);}
}
