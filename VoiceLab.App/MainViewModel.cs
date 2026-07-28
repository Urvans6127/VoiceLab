using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using VoiceLab.App.Services;
using VoiceLab.Audio;
using VoiceLab.Infrastructure;

namespace VoiceLab.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly AudioEngine _engine;
    private readonly PresetStore _store;
    private readonly SettingsCoordinator _settings;
    private readonly FileLogger _logger;
    private readonly LocalizationService _localization;
    private bool _applyingPreset;
    private string _errorKey="";
    private string _customStatusKey="Status.CustomSaved";
    private string _recordingStatusKey="Status.ReadyToRecord";
    private AudioDevice? _input;
    private AudioDevice? _previewOutput;
    private VoicePreset? _preset;
    private bool _isModified;
    private double _inputLevel,_outputLevel,_inputRms,_outputRms;
    private bool _inputClipping,_outputClipping;
    private AudioDiagnostics? _diagnostics;
    private AudioMeterSnapshot? _latestMeter;
    private AudioDiagnostics? _latestDiagnostics;
    private int _meterDispatchPending,_diagnosticDispatchPending;

    public ObservableCollection<AudioDevice> Inputs { get; }=[];
    public ObservableCollection<AudioDevice> PreviewOutputs { get; }=[];
    public ObservableCollection<VoicePreset> Presets { get; }=[];

    public AudioDevice? SelectedInput
    {
        get=>_input;
        set
        {
            if(!Set(ref _input,value))return;
            _settings.Update(settings=>settings with{LastInputDeviceId=value?.Id});
            StartRecordingCommand?.Raise();
        }
    }

    public AudioDevice? SelectedPreviewOutput
    {
        get=>_previewOutput;
        set
        {
            if(!Set(ref _previewOutput,value))return;
            _settings.Update(settings=>settings with{LastPreviewOutputDeviceId=value?.Id});
        }
    }

    public VoicePreset? SelectedPreset
    {
        get=>_preset;
        set
        {
            if(Set(ref _preset,value)&&value is not null)
            {
                Apply(value);
                _settings.Update(settings=>settings with{LastSelectedPreset=value.Name});
                OnChanged(nameof(PresetKind));
            }
        }
    }

    public string Error=>string.IsNullOrEmpty(_errorKey)?"":L(_errorKey);
    public string CustomStatus=>L(_customStatusKey);
    public string RecordingStatus=>L(_recordingStatusKey);
    public bool IsRecording=>_engine.IsRecording;
    public bool IsRecordingPaused=>_engine.IsRecordingPaused;
    public bool IsPreviewing=>_engine.IsPreviewing;
    public bool IsModified{get=>_isModified;private set{if(Set(ref _isModified,value))OnChanged(nameof(PresetKind));}}
    public string PresetKind=>IsModified?L("Status.ModifiedUnsaved"):SelectedPreset?.IsBuiltIn==true?L("Common.BuiltIn"):L("Common.Custom");
    public double InputLevel{get=>_inputLevel;private set=>Set(ref _inputLevel,value);}
    public double OutputLevel{get=>_outputLevel;private set=>Set(ref _outputLevel,value);}
    public double InputRms{get=>_inputRms;private set=>Set(ref _inputRms,value);}
    public double OutputRms{get=>_outputRms;private set=>Set(ref _outputRms,value);}
    public bool InputClipping{get=>_inputClipping;private set=>Set(ref _inputClipping,value);}
    public bool OutputClipping{get=>_outputClipping;private set=>Set(ref _outputClipping,value);}
    public string ClippingWarning=>L(InputClipping||OutputClipping?"Status.Clipping":"Status.NoClipping");
    public string Format=>$"{_engine.SampleRate} Hz";
    public string Latency=>$"{_engine.EstimatedLatencyMs:F0} ms";
    public string SelectedLatencyProfile
    {
        get=>_settings.Current.LatencyProfile;
        set
        {
            var profile=LatencyProfileSettings.For(LatencyProfileSettings.Parse(value));
            _settings.Update(settings=>settings with{LatencyProfile=profile.DisplayName,RequestedBufferMilliseconds=profile.RequestedBufferMilliseconds});
            OnChanged();
            OnChanged(nameof(SelectedLatencyProfileDisplay));
            OnChanged(nameof(RequestedBuffer));
        }
    }
    public string SelectedLatencyProfileDisplay=>L(SelectedLatencyProfile switch{"Safe"=>"Latency.Safe","Low Latency"=>"Latency.Low",_=>"Latency.Balanced"});
    public string RequestedBuffer=>_localization.Format("Format.RequestedMs",LatencyProfileSettings.For(LatencyProfileSettings.Parse(SelectedLatencyProfile)).RequestedBufferMilliseconds);
    public int PreferredSampleRate{get=>_settings.Current.PreferredSampleRate;set{_settings.Update(settings=>settings with{PreferredSampleRate=value});OnChanged();}}
    public string ActiveInput=>_diagnostics?.ActiveInput??L("Status.NotActive");
    public string ActiveBuffer=>_diagnostics is null?L("Status.NotActive"):_localization.Format("Format.ActiveMs",_diagnostics.ActiveBufferMilliseconds);
    public string DiagnosticStatus=>_diagnostics is null?L("Status.DiagnosticsPending"):L("Status.DiagnosticsActive");
    public string DeviceLossStatus=>L(_diagnostics?.DeviceLost==true?"Status.DeviceLost":"Status.NoDeviceLoss");
    public string DroppedStatus=>_engine.RecordingDroppedBuffers>0?_localization.Format("Format.RecordingDrops",_engine.RecordingDroppedBuffers):L("Status.NoRecordingDrops");
    public string RecordingDuration=>$"{_engine.RecordedDuration:hh\\:mm\\:ss}";
    public string CurrentRecordingFile=>_engine.LastRecordingPath??L("Status.NoRecording");
    public string RecordingFolder=>_settings.Current.RecordingFolder;
    public string RecordingDropWarning=>DroppedStatus;

    public AsyncRelayCommand RefreshCommand{get;}
    public RelayCommand RestoreCommand{get;}
    public RelayCommand SaveCustomCommand{get;}
    public RelayCommand ResetCustomCommand{get;}
    public AsyncRelayCommand StartRecordingCommand{get;}
    public AsyncRelayCommand StopRecordingCommand{get;}
    public RelayCommand PauseRecordingCommand{get;}
    public RelayCommand ResumeRecordingCommand{get;}

    public MainViewModel(AudioEngine engine,PresetStore store,SettingsCoordinator settings,FileLogger logger,LocalizationService localization)
    {
        _engine=engine;_store=store;_settings=settings;_logger=logger;_localization=localization;
        _localization.LanguageChanged+=(_,_)=>Dispatch(()=>OnChanged(null));
        _engine.DiagnosticTiming+=(operation,elapsed)=>_logger.LogDiagnostic(operation,elapsed);
        RefreshCommand=new(RefreshAsync);
        RestoreCommand=new(()=>Restore());
        SaveCustomCommand=new(SaveCustom);
        ResetCustomCommand=new(ResetCustom);
        StartRecordingCommand=new(StartRecordingAsync,()=>SelectedInput is not null&&!IsRecording&&_engine.State is AudioEngineState.Stopped or AudioEngineState.Faulted);
        StopRecordingCommand=new(StopRecordingAsync,()=>IsRecording);
        PauseRecordingCommand=new(PauseRecording,()=>IsRecording&&!IsRecordingPaused);
        ResumeRecordingCommand=new(ResumeRecording,()=>IsRecording&&IsRecordingPaused);

        _engine.StateChanged+=(state,error)=>Dispatch(()=>
        {
            if(error is not null)SetError("Error.AudioEngine");
            OnChanged(nameof(Format));OnChanged(nameof(Latency));OnChanged(nameof(IsRecording));OnChanged(nameof(IsPreviewing));
            RaiseRecordingCommands();
        });
        _engine.MeterUpdated+=QueueMeterUpdate;
        _engine.DiagnosticsChanged+=QueueDiagnosticUpdate;
        _engine.RecordingStatusChanged+=recording=>Dispatch(()=>
        {
            SetRecordingStatus(recording.Error is not null?"Status.RecordingFailed":recording.State switch
            {
                RecordingState.Recording=>"Status.Recording",
                RecordingState.Paused=>"Status.RecordingPaused",
                RecordingState.Stopped when _engine.LastRecordingPath is not null=>"Status.RecordingSaved",
                _=>"Status.ReadyToRecord"
            });
            OnChanged(nameof(IsRecording));OnChanged(nameof(IsRecordingPaused));OnChanged(nameof(RecordingDuration));
            OnChanged(nameof(CurrentRecordingFile));OnChanged(nameof(RecordingDropWarning));OnChanged(nameof(DroppedStatus));
            RaiseRecordingCommands();
        });
        _engine.TechnicalError+=exception=>_logger.Log("Audio engine error",exception);
        _engine.DevicesChanged+=()=>Dispatch(()=>RefreshCommand.Execute(null));

        Refresh();
        Restore(false);
    }

    public float InputGain{get=>_engine.InputGain.Gain;set=>Change(()=>_engine.InputGain.Gain=value,_engine.InputGain.Gain,value);}
    public float OutputGain{get=>_engine.OutputGain.Gain;set=>Change(()=>_engine.OutputGain.Gain=value,_engine.OutputGain.Gain,value);}
    public bool GateEnabled{get=>_engine.NoiseGate.IsEnabled;set=>Change(()=>_engine.NoiseGate.IsEnabled=value,_engine.NoiseGate.IsEnabled,value);}
    public float GateThreshold{get=>_engine.NoiseGate.ThresholdDb;set=>Change(()=>_engine.NoiseGate.ThresholdDb=value,_engine.NoiseGate.ThresholdDb,value);}
    public bool PitchEnabled{get=>_engine.Pitch.IsEnabled;set=>Change(()=>_engine.Pitch.IsEnabled=value,_engine.Pitch.IsEnabled,value);}
    public float PitchSemitones
    {
        get=>_engine.Pitch.Semitones;
        set
        {
            if(_engine.Pitch.Semitones.Equals(value))return;
            _engine.Pitch.Semitones=value;
            _engine.Pitch.IsEnabled=Math.Abs(value)>.01f;
            OnChanged();OnChanged(nameof(PitchEnabled));MarkEdited();
        }
    }
    public float VoiceDepth{get=>_engine.ToneShape.VoiceDepth;set=>Change(()=>_engine.ToneShape.VoiceDepth=value,_engine.ToneShape.VoiceDepth,value);}
    public float Brightness{get=>_engine.ToneShape.Brightness;set=>Change(()=>_engine.ToneShape.Brightness=value,_engine.ToneShape.Brightness,value);}
    public float Bass{get=>_engine.ToneShape.BassDb;set=>Change(()=>_engine.ToneShape.BassDb=value,_engine.ToneShape.BassDb,value);}
    public float Treble{get=>_engine.ToneShape.TrebleDb;set=>Change(()=>_engine.ToneShape.TrebleDb=value,_engine.ToneShape.TrebleDb,value);}
    public float ToneMix{get=>_engine.ToneShape.Mix;set=>Change(()=>_engine.ToneShape.Mix=value,_engine.ToneShape.Mix,value);}
    public float Saturation{get=>_engine.ToneShape.Saturation;set=>Change(()=>_engine.ToneShape.Saturation=value,_engine.ToneShape.Saturation,value);}
    public bool RobotEnabled{get=>_engine.Robot.IsEnabled;set=>Change(()=>_engine.Robot.IsEnabled=value,_engine.Robot.IsEnabled,value);}
    public float RobotMix{get=>_engine.Robot.Mix;set=>Change(()=>_engine.Robot.Mix=value,_engine.Robot.Mix,value);}
    public bool EchoEnabled{get=>_engine.Echo.IsEnabled;set=>Change(()=>_engine.Echo.IsEnabled=value,_engine.Echo.IsEnabled,value);}
    public float EchoMix{get=>_engine.Echo.Mix;set=>Change(()=>_engine.Echo.Mix=value,_engine.Echo.Mix,value);}
    public bool ReverbEnabled{get=>_engine.Reverb.IsEnabled;set=>Change(()=>_engine.Reverb.IsEnabled=value,_engine.Reverb.IsEnabled,value);}
    public float ReverbMix{get=>_engine.Reverb.Mix;set=>Change(()=>_engine.Reverb.Mix=value,_engine.Reverb.Mix,value);}

    private async Task StartRecordingAsync()
    {
        try
        {
            SetError("");
            if(SelectedInput is null||!Inputs.Any(device=>device.Id==SelectedInput.Id))
            {
                SetError("Error.SelectMicrophoneRecording");SetRecordingStatus("Status.CouldNotStartRecording");return;
            }
            await _engine.StartAsync(AudioEngineStartOptions.ForRecording(SelectedInput.Id,LatencyProfileSettings.Parse(SelectedLatencyProfile),PreferredSampleRate));
            await _engine.StartRecordingAsync(_settings.Current.RecordingFolder);
            SetRecordingStatus("Status.Recording");
            OnChanged(nameof(IsRecording));
            RaiseRecordingCommands();
        }
        catch(IOException){await StopFailedStartup();SetError("Error.RecordingFolder");SetRecordingStatus("Status.CouldNotStartRecording");}
        catch(UnauthorizedAccessException){await StopFailedStartup();SetError("Error.RecordingFolderAccess");SetRecordingStatus("Status.CouldNotStartRecording");}
        catch(InvalidOperationException){await StopFailedStartup();SetError("Error.StartRecording");SetRecordingStatus("Status.CouldNotStartRecording");}
        catch(Exception exception){await StopFailedStartup();_logger.Log("Recording startup failed",exception);SetError("Error.StartRecording");SetRecordingStatus("Status.CouldNotStartRecording");}
    }

    private async Task StopRecordingAsync()
    {
        await _engine.StopRecordingAsync();
        await _engine.StopAsync();
        SetRecordingStatus(_engine.LastRecordingPath is null?"Status.ReadyToRecord":"Status.RecordingSaved");
        OnChanged(nameof(IsRecording));OnChanged(nameof(IsRecordingPaused));OnChanged(nameof(CurrentRecordingFile));
        RaiseRecordingCommands();
    }

    private void PauseRecording(){_engine.PauseRecording();OnChanged(nameof(IsRecordingPaused));RaiseRecordingCommands();}
    private void ResumeRecording(){_engine.ResumeRecording();OnChanged(nameof(IsRecordingPaused));RaiseRecordingCommands();}
    private Task StopFailedStartup()=>_engine.State==AudioEngineState.Stopped?Task.CompletedTask:_engine.StopAsync();

    private void RaiseRecordingCommands()
    {
        StartRecordingCommand.Raise();StopRecordingCommand.Raise();PauseRecordingCommand.Raise();ResumeRecordingCommand.Raise();
    }

    private void Refresh()
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        try{ApplyDeviceSnapshot(SelectedInput?.Id??_settings.Current.LastInputDeviceId,SelectedPreviewOutput?.Id??_settings.Current.LastPreviewOutputDeviceId,_engine.GetInputDevices(),_engine.GetPreviewOutputDevices());}
        finally{_logger.LogDiagnostic("Device refresh",timer.Elapsed);}
    }

    private async Task RefreshAsync()
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        var inputId=SelectedInput?.Id??_settings.Current.LastInputDeviceId;
        var outputId=SelectedPreviewOutput?.Id??_settings.Current.LastPreviewOutputDeviceId;
        try
        {
            var devices=await Task.Run(()=>(_engine.GetInputDevices(),_engine.GetPreviewOutputDevices()));
            await DispatchAsync(()=>ApplyDeviceSnapshot(inputId,outputId,devices.Item1,devices.Item2));
        }
        catch(Exception exception)
        {
            _logger.Log("Device refresh failed",exception);
            await DispatchAsync(()=>SetError("Error.RefreshDevices"));
        }
        finally{_logger.LogDiagnostic("Device refresh",timer.Elapsed);}
    }

    private void ApplyDeviceSnapshot(string? inputId,string? outputId,IReadOnlyList<AudioDevice> inputs,IReadOnlyList<AudioDevice> outputs)
    {
        Inputs.Clear();
        foreach(var device in inputs)Inputs.Add(device);
        SelectedInput=Inputs.FirstOrDefault(device=>device.Id==inputId)??(string.IsNullOrWhiteSpace(inputId)?Inputs.FirstOrDefault():null);
        PreviewOutputs.Clear();
        foreach(var device in outputs)PreviewOutputs.Add(device);
        SelectedPreviewOutput=PreviewOutputs.FirstOrDefault(device=>device.Id==outputId)??(string.IsNullOrWhiteSpace(outputId)?PreviewOutputs.FirstOrDefault():null);
        SetError(Inputs.Count==0?"Error.NoMicrophone":inputId is not null&&SelectedInput is null?"Error.SavedMicrophone":"");
    }

    private void Apply(VoicePreset preset)
    {
        _applyingPreset=true;
        try
        {
            InputGain=preset.InputGain;OutputGain=preset.OutputGain;
            GateEnabled=preset.GateEnabled;GateThreshold=preset.GateThresholdDb;
            _engine.NoiseGate.AttackMs=preset.GateAttackMs;_engine.NoiseGate.ReleaseMs=preset.GateReleaseMs;
            PitchEnabled=preset.PitchEnabled;PitchSemitones=preset.PitchSemitones;
            VoiceDepth=preset.VoiceDepth;Brightness=preset.Brightness;Bass=preset.BassDb;Treble=preset.TrebleDb;ToneMix=preset.ToneMix;Saturation=preset.Saturation;
            RobotEnabled=preset.RobotEnabled;_engine.Robot.CarrierFrequency=preset.RobotFrequency;RobotMix=preset.RobotMix;
            EchoEnabled=preset.EchoEnabled;_engine.Echo.DelayMs=preset.EchoDelayMs;_engine.Echo.Feedback=preset.EchoFeedback;EchoMix=preset.EchoMix;
            ReverbEnabled=preset.ReverbEnabled;_engine.Reverb.RoomSize=preset.ReverbRoomSize;ReverbMix=preset.ReverbMix;
        }
        finally{_applyingPreset=false;}
        SetCustomStatus(PresetStore.IsCustom(preset)?"Status.CustomLoaded":"Status.BuiltInEdit");
        IsModified=false;
    }

    private VoicePreset Snapshot(string name,bool isBuiltIn=false)=>new(name,isBuiltIn,InputGain,OutputGain,GateEnabled,GateThreshold,_engine.NoiseGate.AttackMs,_engine.NoiseGate.ReleaseMs,PitchEnabled,PitchSemitones,RobotEnabled,_engine.Robot.CarrierFrequency,RobotMix,EchoEnabled,_engine.Echo.DelayMs,_engine.Echo.Feedback,EchoMix,ReverbEnabled,_engine.Reverb.RoomSize,ReverbMix,VoiceDepth,Brightness,Bass,Treble,ToneMix,Saturation);

    private void MarkEdited()
    {
        if(_applyingPreset)return;
        if(SelectedPreset is not null&&!PresetStore.IsCustom(SelectedPreset))
        {
            _preset=PresetStore.SelectForEdit(SelectedPreset,Presets);
            OnChanged(nameof(SelectedPreset));
            _settings.Update(settings=>settings with{LastSelectedPreset=_preset.Name});
        }
        SetCustomStatus("Status.CustomUnsaved");IsModified=true;
    }

    private void SaveCustom()
    {
        var index=FindCustomIndex();var saved=Snapshot(PresetStore.CustomName);Presets[index]=saved;_preset=saved;
        OnChanged(nameof(SelectedPreset));Persist();SetCustomStatus("Status.CustomSaved");IsModified=false;
    }

    private void ResetCustom()
    {
        var index=FindCustomIndex();var reset=PresetStore.CreateDefaultCustom();Presets[index]=reset;SelectedPreset=reset;
        Persist();SetCustomStatus("Status.CustomReset");IsModified=false;
    }

    private int FindCustomIndex()
    {
        var index=Presets.ToList().FindIndex(PresetStore.IsCustom);
        if(index>=0)return index;
        Presets.Add(PresetStore.CreateDefaultCustom());return Presets.Count-1;
    }

    private void Delete()
    {
        if(SelectedPreset is null||SelectedPreset.IsBuiltIn||PresetStore.IsCustom(SelectedPreset))return;
        Presets.Remove(SelectedPreset);SelectedPreset=Presets[0];Persist();
    }

    private void Restore(bool persist=true)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var customPresets=persist?Presets.Where(preset=>!preset.IsBuiltIn).ToArray():[];
            Presets.Clear();
            var restored=persist
                ?PresetStore.Defaults().Where(preset=>preset.IsBuiltIn).Concat(customPresets.Length==0?[PresetStore.CreateDefaultCustom()]:customPresets)
                :_store.Load();
            foreach(var preset in restored)Presets.Add(preset);
            SelectedPreset=Presets.FirstOrDefault(preset=>string.Equals(preset.Name,_settings.Current.LastSelectedPreset,StringComparison.OrdinalIgnoreCase))??Presets.FirstOrDefault();
            if(persist)Persist();
        }
        finally{_logger.LogDiagnostic("Preset load",timer.Elapsed);}
    }

    public void SaveAsNamed(string name){PresetTransferService.ValidateName(name,Presets);var preset=Snapshot(name.Trim());Presets.Add(preset);SelectedPreset=preset;Persist();IsModified=false;}
    public void DuplicateSelected(string name){if(SelectedPreset is null)return;PresetTransferService.ValidateName(name,Presets);var duplicate=PresetStore.Sanitize(SelectedPreset with{Name=name.Trim(),IsBuiltIn=false});Presets.Add(duplicate);SelectedPreset=duplicate;Persist();IsModified=false;}
    public void RenameSelected(string name){if(SelectedPreset is null||SelectedPreset.IsBuiltIn||PresetStore.IsCustom(SelectedPreset))throw new PresetValidationException(nameof(RenameSelected));PresetTransferService.ValidateName(name,Presets,SelectedPreset);var index=Presets.IndexOf(SelectedPreset);var renamed=SelectedPreset with{Name=name.Trim()};Presets[index]=renamed;SelectedPreset=renamed;Persist();}
    public void DeleteSelectedCustom()=>Delete();
    public void AddImported(VoicePreset preset){Presets.Add(preset);SelectedPreset=preset;Persist();}
    public void ShowErrorKey(string key)=>SetError(key);
    public void NotifyRecordingFolderChanged()=>OnChanged(nameof(RecordingFolder));

    private void Persist(){try{_store.Save(Presets);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){SetError("Error.SavePreset");}}
    private string L(string key)=>_localization.Get(key);
    private void SetError(string key){if(_errorKey==key)return;_errorKey=key;OnChanged(nameof(Error));}
    private void SetCustomStatus(string key){if(_customStatusKey==key)return;_customStatusKey=key;OnChanged(nameof(CustomStatus));}
    private void SetRecordingStatus(string key){if(_recordingStatusKey==key)return;_recordingStatusKey=key;OnChanged(nameof(RecordingStatus));}
    private void Change<T>(Action update,T current,T value,[CallerMemberName]string? name=null){if(EqualityComparer<T>.Default.Equals(current,value))return;update();OnChanged(name);MarkEdited();}
    private static void Dispatch(Action action){var dispatcher=System.Windows.Application.Current?.Dispatcher;if(dispatcher is null||dispatcher.CheckAccess())action();else dispatcher.BeginInvoke(action);}
    private static Task DispatchAsync(Action action){var dispatcher=System.Windows.Application.Current?.Dispatcher;if(dispatcher is null||dispatcher.CheckAccess()){action();return Task.CompletedTask;}return dispatcher.InvokeAsync(action).Task;}
    private void QueueMeterUpdate(AudioMeterSnapshot meter){_latestMeter=meter;if(Interlocked.Exchange(ref _meterDispatchPending,1)!=0)return;Dispatch(()=>{var value=_latestMeter;if(value is not null){InputLevel=value.InputPeak*100;OutputLevel=value.OutputPeak*100;InputRms=value.InputRms*100;OutputRms=value.OutputRms*100;InputClipping=value.InputClipping;OutputClipping=value.OutputClipping;OnChanged(nameof(ClippingWarning));OnChanged(nameof(RecordingDuration));OnChanged(nameof(RecordingDropWarning));}Interlocked.Exchange(ref _meterDispatchPending,0);});}
    private void QueueDiagnosticUpdate(AudioDiagnostics diagnostics){_latestDiagnostics=diagnostics;if(Interlocked.Exchange(ref _diagnosticDispatchPending,1)!=0)return;Dispatch(()=>{_diagnostics=_latestDiagnostics;OnChanged(nameof(ActiveInput));OnChanged(nameof(ActiveBuffer));OnChanged(nameof(DiagnosticStatus));OnChanged(nameof(DeviceLossStatus));OnChanged(nameof(DroppedStatus));OnChanged(nameof(Format));OnChanged(nameof(Latency));Interlocked.Exchange(ref _diagnosticDispatchPending,0);});}
    private bool Set<T>(ref T field,T value,[CallerMemberName]string? name=null){if(EqualityComparer<T>.Default.Equals(field,value))return false;field=value;OnChanged(name);return true;}
    private void OnChanged([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
    public event PropertyChangedEventHandler? PropertyChanged;
}
