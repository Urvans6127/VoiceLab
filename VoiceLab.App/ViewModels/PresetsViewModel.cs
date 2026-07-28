using VoiceLab.App.Services;
using VoiceLab.Audio;
using VoiceLab.Infrastructure;
using System.ComponentModel;
using System.IO;

namespace VoiceLab.App.ViewModels;

public sealed class PresetsViewModel : ViewModelBase
{
    private readonly MainViewModel _workspace;
    private readonly AudioEngine _engine;
    private readonly PresetTransferService _transfer;
    private readonly FileDialogService _dialogs;
    private readonly FileLogger _logger;
    private readonly LocalizationService _localization;
    private string _presetName = "";
    private string _previewStatusKey="Preview.Stopped";
    private bool _previewRequested;
    public MainViewModel Workspace => _workspace;
    public string PresetName { get => _presetName; set => Set(ref _presetName, value); }
    public bool IsPreviewing=>_engine.IsPreviewing;
    public bool CanSelectPreviewDevices=>_engine.State is AudioEngineState.Stopped or AudioEngineState.Faulted;
    public string PreviewStatus=>_localization.Get(_previewStatusKey);
    public RelayCommand DuplicateCommand { get; }
    public RelayCommand SaveAsCommand { get; }
    public RelayCommand RenameCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand ExportCommand { get; }
    public AsyncRelayCommand StartPreviewCommand { get; }
    public AsyncRelayCommand StopPreviewCommand { get; }

    public PresetsViewModel(MainViewModel workspace, AudioEngine engine, PresetTransferService transfer, FileDialogService dialogs, FileLogger logger, LocalizationService localization)
    {
        _workspace=workspace;_engine=engine;_transfer=transfer;_dialogs=dialogs;_logger=logger;_localization=localization;
        _workspace.PropertyChanged+=OnWorkspacePropertyChanged;
        _engine.StateChanged+=OnEngineStateChanged;
        _localization.LanguageChanged+=(_,_)=>Dispatch(()=>Notify(nameof(PreviewStatus)));
        SynchronizePresetName();
        DuplicateCommand=new(Duplicate); SaveAsCommand=new(SaveAs); RenameCommand=new(Rename); DeleteCommand=new(workspace.DeleteSelectedCustom);
        ImportCommand=new(Import); ExportCommand=new(Export);
        StartPreviewCommand=new(StartPreviewAsync,CanStartPreview);
        StopPreviewCommand=new(StopPreviewAsync,CanStopPreview);
    }
    private void Duplicate() => Run(() => _workspace.DuplicateSelected(PresetName));
    private void SaveAs() => Run(() => _workspace.SaveAsNamed(PresetName));
    private void Rename() => Run(() => _workspace.RenameSelected(PresetName));
    private void Import()
    {
        var path=_dialogs.ChoosePresetToOpen(); if(path is null)return;
        Run(() => _workspace.AddImported(_transfer.ImportFile(path, _workspace.Presets)));
    }
    private void Export()
    {
        if(_workspace.SelectedPreset is null)return;
        var path=_dialogs.ChoosePresetToExport(_workspace.SelectedPreset.Name); if(path is null)return;
        Run(() => File.WriteAllText(path, _transfer.Export(_workspace.SelectedPreset)));
    }
    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName==nameof(MainViewModel.SelectedPreset))SynchronizePresetName();
        if(e.PropertyName is nameof(MainViewModel.SelectedInput) or nameof(MainViewModel.SelectedPreviewOutput) or nameof(MainViewModel.IsRecording) or nameof(MainViewModel.IsPreviewing))
        {
            Notify(nameof(IsPreviewing));Notify(nameof(CanSelectPreviewDevices));RaisePreviewCommands();
        }
    }
    private bool CanStartPreview()=>_workspace.SelectedInput is not null&&_workspace.SelectedPreviewOutput is not null&&!_workspace.IsRecording&&_engine.State is AudioEngineState.Stopped or AudioEngineState.Faulted;
    private bool CanStopPreview()=>_previewRequested&&_engine.State is AudioEngineState.Starting or AudioEngineState.Running or AudioEngineState.Faulted;
    private async Task StartPreviewAsync()
    {
        if(_workspace.SelectedInput is null||_workspace.SelectedPreviewOutput is null)return;
        _previewRequested=true;RaisePreviewCommands();
        try
        {
            var started=await _engine.StartAsync(AudioEngineStartOptions.ForPreview(_workspace.SelectedInput.Id,_workspace.SelectedPreviewOutput.Id,LatencyProfileSettings.Parse(_workspace.SelectedLatencyProfile),_workspace.PreferredSampleRate));
            if(started&&_engine.IsPreviewing)SetPreviewStatus("Preview.Active");
            else{_previewRequested=false;SetPreviewStatus("Preview.CouldNotStart");}
        }
        catch(Exception exception)
        {
            _previewRequested=false;_logger.Log("Live preview startup failed",exception);_workspace.ShowErrorKey("Error.Preview");SetPreviewStatus("Preview.CouldNotStart");
        }
        finally{Notify(nameof(IsPreviewing));Notify(nameof(CanSelectPreviewDevices));RaisePreviewCommands();}
    }
    private async Task StopPreviewAsync()
    {
        try{if(_engine.Mode==AudioEngineMode.Preview||_previewRequested)await _engine.StopAsync();}
        catch(Exception exception){_logger.Log("Live preview shutdown failed",exception);}
        finally{_previewRequested=false;SetPreviewStatus("Preview.Stopped");Notify(nameof(IsPreviewing));Notify(nameof(CanSelectPreviewDevices));RaisePreviewCommands();}
    }
    private void OnEngineStateChanged(AudioEngineState state,string? error)=>Dispatch(()=>
    {
        if(_previewRequested&&state==AudioEngineState.Faulted){_previewRequested=false;SetPreviewStatus("Preview.CouldNotStart");}
        else if(_previewRequested&&state==AudioEngineState.Running&&_engine.IsPreviewing)SetPreviewStatus("Preview.Active");
        else if(state==AudioEngineState.Stopped)SetPreviewStatus("Preview.Stopped");
        Notify(nameof(IsPreviewing));Notify(nameof(CanSelectPreviewDevices));RaisePreviewCommands();
    });
    private void SetPreviewStatus(string key){if(_previewStatusKey==key)return;_previewStatusKey=key;Notify(nameof(PreviewStatus));}
    private void RaisePreviewCommands(){StartPreviewCommand?.Raise();StopPreviewCommand?.Raise();}
    private static void Dispatch(Action action){var dispatcher=System.Windows.Application.Current?.Dispatcher;if(dispatcher is null||dispatcher.CheckAccess())action();else dispatcher.BeginInvoke(action);}
    private void SynchronizePresetName()=>PresetName=_workspace.SelectedPreset?.Name??"";
    private void Run(Action action){try{action();SynchronizePresetName();}catch(Exception ex) when(ex is PresetValidationException or IOException or UnauthorizedAccessException){_workspace.ShowErrorKey("Error.PresetOperation");}}
}
