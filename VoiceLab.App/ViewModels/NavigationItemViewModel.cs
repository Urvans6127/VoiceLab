using VoiceLab.App.Services;
using System.ComponentModel;

namespace VoiceLab.App.ViewModels;

public sealed class NavigationItemViewModel : INotifyPropertyChanged
{
    private readonly LocalizationService _localization;
    public string Id { get; }
    public string TitleKey { get; }
    public string Title => _localization.Get(TitleKey);
    public string Glyph { get; }
    public object Page { get; }

    public NavigationItemViewModel(string id, string titleKey, string glyph, object page, LocalizationService localization)
    {
        Id=id;TitleKey=titleKey;Glyph=glyph;Page=page;_localization=localization;
        _localization.LanguageChanged+=(_,_)=>PropertyChanged?.Invoke(this,new(nameof(Title)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
