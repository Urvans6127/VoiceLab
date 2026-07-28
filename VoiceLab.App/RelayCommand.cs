using System.Windows.Input;
namespace VoiceLab.App;
public sealed class RelayCommand(Action run,Func<bool>? allowed=null):ICommand{public event EventHandler? CanExecuteChanged;public bool CanExecute(object? p)=>allowed?.Invoke()??true;public void Execute(object? p)=>run();public void Raise()=>CanExecuteChanged?.Invoke(this,EventArgs.Empty);}
