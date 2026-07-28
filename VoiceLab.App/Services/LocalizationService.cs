using System.Collections;
using System.Globalization;
using System.Resources;
using System.Windows;

namespace VoiceLab.App.Services;

public sealed class LocalizationService
{
    public const string DefaultLanguage = "en";
    private readonly ResourceDictionary _english;
    private ResourceDictionary? _active;
    private IReadOnlyList<LanguageOption>? _languages;

    public string CurrentLanguage { get; private set; } = DefaultLanguage;
    public IReadOnlyList<LanguageOption> Languages => _languages ??= DiscoverLanguages();
    public event EventHandler? LanguageChanged;

    public LocalizationService()
    {
        _english = LoadDictionary(DefaultLanguage);
        ApplyDictionaries(_english, null);
    }

    public void SetLanguage(string? languageCode)
    {
        var requested=Normalize(languageCode);
        if(!Languages.Any(language=>string.Equals(language.Code,requested,StringComparison.OrdinalIgnoreCase)))
            requested=DefaultLanguage;
        ResourceDictionary? selected=null;
        if(!string.Equals(requested,DefaultLanguage,StringComparison.OrdinalIgnoreCase))
            selected=LoadDictionary(requested);
        ApplyDictionaries(_english,selected);
        CurrentLanguage=requested;
        var culture=CultureInfo.GetCultureInfo(requested);
        CultureInfo.CurrentUICulture=culture;
        CultureInfo.CurrentCulture=culture;
        foreach(var language in Languages)language.NotifyLanguageChanged();
        LanguageChanged?.Invoke(this,EventArgs.Empty);
    }

    public string Get(string key)
    {
        if(_active?[key] is string translated)return translated;
        if(_english[key] is string fallback)return fallback;
        return key;
    }

    public string Format(string key,params object?[] arguments)=>string.Format(CultureInfo.CurrentCulture,Get(key),arguments);

    private void ApplyDictionaries(ResourceDictionary english,ResourceDictionary? selected)
    {
        var dictionaries=System.Windows.Application.Current.Resources.MergedDictionaries;
        if(_active is not null)dictionaries.Remove(_active);
        if(!dictionaries.Contains(english))dictionaries.Insert(0,english);
        if(selected is not null){dictionaries.Insert(1,selected);_active=selected;}else _active=null;
    }

    private IReadOnlyList<LanguageOption> DiscoverLanguages()
    {
        var codes=new HashSet<string>(StringComparer.OrdinalIgnoreCase){DefaultLanguage};
        var assembly=typeof(LocalizationService).Assembly;
        using var stream=assembly.GetManifestResourceStream($"{assembly.GetName().Name}.g.resources");
        if(stream is not null)
        {
            using var reader=new ResourceReader(stream);
            foreach(DictionaryEntry entry in reader)
            {
                var name=entry.Key?.ToString();
                const string prefix="resources/strings.";
                const string suffix=".baml";
                if(name is not null&&name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&name.EndsWith(suffix,StringComparison.OrdinalIgnoreCase))
                    codes.Add(name[prefix.Length..^suffix.Length]);
            }
        }
        return codes.Select(code=>new LanguageOption(code,this))
            .OrderBy(option=>option.Code==DefaultLanguage?0:1)
            .ThenBy(option=>option.DisplayName,StringComparer.CurrentCulture)
            .ToArray();
    }

    private static string Normalize(string? code)
    {
        if(string.IsNullOrWhiteSpace(code))return DefaultLanguage;
        var normalized=code.Trim().ToLowerInvariant();
        return normalized.Split('-',StringSplitOptions.RemoveEmptyEntries)[0];
    }

    private static ResourceDictionary LoadDictionary(string code)=>new()
    {
        Source=new Uri($"/VoiceLab;component/Resources/Strings.{code}.xaml",UriKind.Relative)
    };
}

public sealed class LanguageOption(string code,LocalizationService localization):System.ComponentModel.INotifyPropertyChanged
{
    public string Code { get; }=code;
    public string DisplayName
    {
        get
        {
            try{return CultureInfo.GetCultureInfo(Code).NativeName;}
            catch(CultureNotFoundException){return localization.Get($"Language.{Code}");}
        }
    }
    internal void NotifyLanguageChanged()=>PropertyChanged?.Invoke(this,new(nameof(DisplayName)));
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
