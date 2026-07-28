using VoiceLab.App.Services;
using VoiceLab.Infrastructure;
using System.ComponentModel;
using System.IO;

namespace VoiceLab.App.ViewModels;

public sealed class RecordingViewModel:ViewModelBase
{
    private readonly SettingsCoordinator _settings;private readonly FileDialogService _dialogs;private readonly FolderLauncher _launcher;private readonly LocalizationService _localization;
    private bool _isAdvancedControlsExpanded;
    public MainViewModel Workspace{get;}public RelayCommand OpenFolderCommand{get;}public RelayCommand ChooseFolderCommand{get;}public RelayCommand RestoreDefaultFolderCommand{get;}
    public bool IsAdvancedControlsExpanded{get=>_isAdvancedControlsExpanded;set=>Set(ref _isAdvancedControlsExpanded,value);}
    public string AdvancedControlsSummary=>_localization.Format("Dsp.AdvancedSummary",Workspace.PitchSemitones,Workspace.Bass,Workspace.Treble,_localization.Get(Workspace.GateEnabled?"Common.On":"Common.Off"));
    public RecordingViewModel(MainViewModel workspace,SettingsCoordinator settings,FileDialogService dialogs,FolderLauncher launcher,LocalizationService localization)
    {
        Workspace=workspace;_settings=settings;_dialogs=dialogs;_launcher=launcher;_localization=localization;
        Workspace.PropertyChanged+=OnWorkspacePropertyChanged;_localization.LanguageChanged+=(_,_)=>Notify(nameof(AdvancedControlsSummary));
        OpenFolderCommand=new(OpenFolder);ChooseFolderCommand=new(ChooseFolder);RestoreDefaultFolderCommand=new(RestoreDefault);
    }
    private void OnWorkspacePropertyChanged(object? sender,PropertyChangedEventArgs e)
    {
        if(e.PropertyName is nameof(MainViewModel.PitchSemitones) or nameof(MainViewModel.Bass) or nameof(MainViewModel.Treble) or nameof(MainViewModel.GateEnabled) or nameof(MainViewModel.SelectedPreset))
            Notify(nameof(AdvancedControlsSummary));
    }
    private void OpenFolder(){try{Directory.CreateDirectory(_settings.Current.RecordingFolder);_launcher.Open(_settings.Current.RecordingFolder);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){Workspace.ShowErrorKey("Error.RecordingFolderOpen");}}
    private void ChooseFolder(){var folder=_dialogs.ChooseFolder(_settings.Current.RecordingFolder);if(folder is null)return;_settings.Update(s=>s with{RecordingFolder=folder});Workspace.NotifyRecordingFolderChanged();}
    private void RestoreDefault(){_settings.Update(s=>s with{RecordingFolder=new ApplicationSettings().RecordingFolder});Workspace.NotifyRecordingFolderChanged();}
}
