using System.Runtime.ExceptionServices;
using System.Windows.Input;

namespace VoiceLab.App;

public sealed class AsyncRelayCommand(Func<Task> execute,Func<bool>? canExecute=null):ICommand
{
    private int _running;
    public bool CanExecute(object? parameter)=>Volatile.Read(ref _running)==0&&(canExecute?.Invoke()??true);
    public void Execute(object? parameter)=>_ = ExecuteAndObserveAsync();
    public async Task ExecuteAsync()
    {
        if(Interlocked.Exchange(ref _running,1)!=0)return;
        Raise();
        try{await execute();}
        finally{Interlocked.Exchange(ref _running,0);Raise();}
    }
    private async Task ExecuteAndObserveAsync()
    {
        try{await ExecuteAsync();}
        catch(OperationCanceledException){ }
        catch(Exception ex)
        {
            var dispatcher=System.Windows.Application.Current?.Dispatcher;
            if(dispatcher is not null)_=dispatcher.BeginInvoke(new Action(()=>ExceptionDispatchInfo.Capture(ex).Throw()));
            else ThreadPool.QueueUserWorkItem(_=>ExceptionDispatchInfo.Capture(ex).Throw());
        }
    }
    public void Raise()=>CanExecuteChanged?.Invoke(this,EventArgs.Empty);
    public event EventHandler? CanExecuteChanged;
}
