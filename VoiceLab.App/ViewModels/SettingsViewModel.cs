using VoiceLab.App.Services;

namespace VoiceLab.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsCoordinator _settings;
    private readonly FileDialogService _dialogs;
    private readonly LocalizationService _localization;
    public string RecordingFolder => _settings.Current.RecordingFolder;
    public IReadOnlyList<LanguageOption> Languages => _localization.Languages;
    public LanguageOption SelectedLanguage
    {
        get => Languages.FirstOrDefault(language => string.Equals(language.Code, _localization.CurrentLanguage, StringComparison.OrdinalIgnoreCase)) ?? Languages[0];
        set
        {
            if (value is null || string.Equals(value.Code, _localization.CurrentLanguage, StringComparison.OrdinalIgnoreCase)) return;
            _localization.SetLanguage(value.Code);
            _settings.Update(settings => settings with { Language = _localization.CurrentLanguage });
            Notify();
        }
    }
    public RelayCommand ChooseRecordingFolderCommand { get; }
    public SettingsViewModel(SettingsCoordinator settings, FileDialogService dialogs, LocalizationService localization)
    {
        _settings = settings; _dialogs = dialogs; _localization = localization;
        _localization.LanguageChanged += (_, _) => Notify(nameof(SelectedLanguage));
        ChooseRecordingFolderCommand = new(ChooseFolder);
    }
    private void ChooseFolder()
    {
        var folder = _dialogs.ChooseFolder(RecordingFolder);
        if (folder is null) return;
        _settings.Update(s => s with { RecordingFolder = folder }); Notify(nameof(RecordingFolder));
    }
}
