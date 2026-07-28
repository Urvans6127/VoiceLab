using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VoiceLab.App.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; PropertyChanged?.Invoke(this, new(name)); return true;
    }
    protected void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public event PropertyChangedEventHandler? PropertyChanged;
}
