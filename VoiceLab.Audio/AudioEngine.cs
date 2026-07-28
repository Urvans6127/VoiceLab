using VoiceLab.Effects;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VoiceLab.Audio;

public sealed class AudioEngine : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator=new();
    private readonly EngineLifecycleCoordinator _lifecycle=new();
    private readonly AudioMeterAccumulator _meter=new();
    private readonly ThrottledMeterPublisher _meterPublisher;
    private readonly RecordingPipeline _recording=new();
    private readonly EndpointNotificationClient _notifications;
    private WasapiCapture? _capture;private WasapiOut? _previewOutput;private BufferedWaveProvider? _captureBuffer;private SmbPitchShiftingSampleProvider? _pitchProvider;private CancellationTokenSource? _recordingOnlyPumpCancellation;private Task? _recordingOnlyPump;
    private long _captureOverflows;private int _faultScheduled;private string? _activeInput,_activeInputId,_activePreviewOutputId,_lastError;private bool _deviceLost;private int _requestedBuffer;private double _activeBuffer;private AudioEngineMode _mode=AudioEngineMode.Recording;

    public GainEffect InputGain{get;}=new();public NoiseGateEffect NoiseGate{get;}=new();public PitchEffect Pitch{get;}=new();public ToneShapingEffect ToneShape{get;}=new();public RobotEffect Robot{get;}=new();public EchoEffect Echo{get;}=new();public ReverbEffect Reverb{get;}=new();public GainEffect OutputGain{get;}=new();
    public AudioEngineState State=>_lifecycle.State;public AudioEngineMode Mode=>_mode;public int SampleRate{get;private set;}public double EstimatedLatencyMs{get;private set;}public bool IsPreviewing=>State==AudioEngineState.Running&&_mode==AudioEngineMode.Preview;public bool IsRecording=>_recording.State is RecordingState.Recording or RecordingState.Paused;public bool IsRecordingPaused=>_recording.State==RecordingState.Paused;public string? LastRecordingPath=>_recording.FilePath;public TimeSpan RecordedDuration=>_recording.RecordedDuration;public long RecordingDroppedBuffers=>_recording.DroppedBuffers;
    public event Action<float,float>? LevelsChanged;public event Action<AudioMeterSnapshot>? MeterUpdated;public event Action<AudioEngineState,string?>? StateChanged;public event Action<AudioDiagnostics>? DiagnosticsChanged;public event Action<RecordingStatus>? RecordingStatusChanged;public event Action<Exception>? TechnicalError;public event Action? DevicesChanged;public event Action<string,TimeSpan>? DiagnosticTiming;

    public AudioEngine()
    {
        _meterPublisher=new(_meter);_meterPublisher.Published+=OnMeterPublished;
        _notifications=new(()=>DevicesChanged?.Invoke(),OnEndpointUnavailable);_enumerator.RegisterEndpointNotificationCallback(_notifications);
        _lifecycle.StateChanged+=(state,error)=>{if(error is not null){_lastError=Friendly(error);TechnicalError?.Invoke(error);}StateChanged?.Invoke(state,error is null?null:Friendly(error));PublishDiagnostics();};
        _recording.StatusChanged+=status=>RecordingStatusChanged?.Invoke(status);
    }

    public IReadOnlyList<AudioDevice> GetInputDevices()=>_enumerator.EnumerateAudioEndPoints(DataFlow.Capture,DeviceState.Active).Select(device=>new AudioDevice(device.ID,device.FriendlyName)).ToArray();
    public IReadOnlyList<AudioDevice> GetPreviewOutputDevices()=>_enumerator.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active).Select(device=>new AudioDevice(device.ID,device.FriendlyName)).ToArray();

    public Task<bool> StartAsync(AudioEngineStartOptions options,CancellationToken cancellationToken=default)=>_lifecycle.StartAsync(token=>Task.Run(()=>Initialize(options,token),token),CleanupAsync,cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken=default)=>_lifecycle.StopAsync(CleanupAsync,cancellationToken);
    public void Start(string inputId,int latencyMs=50)=>StartAsync(new(inputId,ClosestProfile(latencyMs))).GetAwaiter().GetResult();
    public void Stop()=>StopAsync().GetAwaiter().GetResult();

    private void Initialize(AudioEngineStartOptions options,CancellationToken token)
    {
        var startupTimer=System.Diagnostics.Stopwatch.StartNew();
        WasapiCapture? capture=null;WasapiOut? previewOutput=null;var transferred=false;
        try
        {
            var profile=LatencyProfileSettings.For(options.Profile);_requestedBuffer=profile.RequestedBufferMilliseconds;_activeBuffer=_requestedBuffer;_lastError=null;_deviceLost=false;Interlocked.Exchange(ref _captureOverflows,0);Interlocked.Exchange(ref _faultScheduled,0);
            var input=_enumerator.GetDevice(options.InputDeviceId);
            MMDevice? output=null;
            if(options.Mode==AudioEngineMode.Preview)
            {
                if(string.IsNullOrWhiteSpace(options.PreviewOutputDeviceId))throw new ArgumentException("A preview playback device is required.",nameof(options));
                output=_enumerator.GetDevice(options.PreviewOutputDeviceId);
            }
            token.ThrowIfCancellationRequested();capture=new WasapiCapture(input,true,_requestedBuffer);var mixFormat=capture.WaveFormat;
            if(options.PreferredSampleRate>0&&mixFormat.SampleRate!=options.PreferredSampleRate)try{var preferred=WaveFormat.CreateIeeeFloatWaveFormat(options.PreferredSampleRate,mixFormat.Channels);using var client=input.AudioClient;if(client.IsFormatSupported(AudioClientShareMode.Shared,preferred))capture.WaveFormat=preferred;}catch(ArgumentException){}
            var format=capture.WaveFormat;SampleRate=format.SampleRate;
            if(options.PreferredSampleRate>0&&format.SampleRate!=options.PreferredSampleRate)_lastError=$"The capture endpoint uses {format.SampleRate} Hz in shared mode; preferred {options.PreferredSampleRate} Hz was unavailable.";
            var captureBuffer=new BufferedWaveProvider(format){BufferDuration=TimeSpan.FromMilliseconds(Math.Max(250,_requestedBuffer*5)),DiscardOnBufferOverflow=true,ReadFully=true};
            ISampleProvider inputStage=new InputStageSampleProvider(captureBuffer.ToSampleProvider(),new(InputGain,NoiseGate),_meter);
            var pitch=new SmbPitchShiftingSampleProvider(inputStage);
            var fanout=new FanoutSampleProvider(pitch,new(ToneShape,Robot,Echo,Reverb,OutputGain),this);
            if(output is not null)
            {
                previewOutput=new WasapiOut(output,AudioClientShareMode.Shared,true,_requestedBuffer);
                previewOutput.Init(fanout);
                previewOutput.PlaybackStopped+=OnPreviewPlaybackStopped;
            }
            token.ThrowIfCancellationRequested();
            _capture=capture;_previewOutput=previewOutput;_captureBuffer=captureBuffer;_pitchProvider=pitch;_activeInput=input.FriendlyName;_activeInputId=input.ID;_activePreviewOutputId=output?.ID;_mode=options.Mode;
            transferred=true;
            capture.DataAvailable+=OnCaptureData;capture.RecordingStopped+=OnCaptureStopped;
            capture.StartRecording();
            if(previewOutput is not null)previewOutput.Play();else StartRecordingOnlyPump(fanout,format);
            EstimatedLatencyMs=_requestedBuffer+captureBuffer.BufferedDuration.TotalMilliseconds;_meterPublisher.Start();PublishDiagnostics();
        }
        finally
        {
            if(!transferred)
            {
                if(previewOutput is not null){previewOutput.PlaybackStopped-=OnPreviewPlaybackStopped;previewOutput.Dispose();}
                capture?.Dispose();
            }
            PublishTiming("Audio engine startup",startupTimer.Elapsed);
        }
    }

    private async Task CleanupAsync()
    {
        var shutdownTimer=System.Diagnostics.Stopwatch.StartNew();
        var pumpCancellation=Interlocked.Exchange(ref _recordingOnlyPumpCancellation,null);pumpCancellation?.Cancel();var pump=Interlocked.Exchange(ref _recordingOnlyPump,null);if(pump is not null)try{await pump.ConfigureAwait(false);}catch(OperationCanceledException){}pumpCancellation?.Dispose();
        var previewOutput=Interlocked.Exchange(ref _previewOutput,null);
        var componentTimer=System.Diagnostics.Stopwatch.StartNew();if(previewOutput is not null){previewOutput.PlaybackStopped-=OnPreviewPlaybackStopped;CleanupComponent(previewOutput.Stop);CleanupComponent(previewOutput.Dispose);}PublishTiming("Preview playback disposal",componentTimer.Elapsed);
        componentTimer.Restart();_meterPublisher.Stop();PublishTiming("Meter timer disposal",componentTimer.Elapsed);
        var capture=Interlocked.Exchange(ref _capture,null);
        componentTimer.Restart();if(capture is not null){capture.DataAvailable-=OnCaptureData;capture.RecordingStopped-=OnCaptureStopped;CleanupComponent(()=>capture.StopRecording());CleanupComponent(capture.Dispose);}PublishTiming("Audio capture disposal",componentTimer.Elapsed);
        componentTimer.Restart();await _recording.StopAsync().ConfigureAwait(false);PublishTiming("Recording finalization",componentTimer.Elapsed);
        _captureBuffer=null;_pitchProvider=null;_activeInput=null;_activeInputId=null;_activePreviewOutputId=null;_mode=AudioEngineMode.Recording;
        InputGain.Reset();NoiseGate.Reset();ToneShape.Reset();Robot.Reset();Echo.Reset();Reverb.Reset();OutputGain.Reset();
        PublishTiming("Audio engine shutdown",shutdownTimer.Elapsed);
    }

    public async Task<string> StartRecordingAsync(string directory,CancellationToken cancellationToken=default)
    {
        if(State!=AudioEngineState.Running||_pitchProvider is null||_mode!=AudioEngineMode.Recording)throw new InvalidOperationException("Start the recording engine before recording.");
        var timer=System.Diagnostics.Stopwatch.StartNew();
        try{return await _recording.StartAsync(directory,_pitchProvider.WaveFormat,cancellationToken).ConfigureAwait(false);}
        finally{PublishTiming("Recording startup",timer.Elapsed);}
    }
    public string StartRecording(string directory)=>StartRecordingAsync(directory).GetAwaiter().GetResult();
    public void PauseRecording()=>_recording.Pause();public void ResumeRecording()=>_recording.Resume();public async Task StopRecordingAsync(){var timer=System.Diagnostics.Stopwatch.StartNew();try{await _recording.StopAsync().ConfigureAwait(false);}finally{PublishTiming("Recording shutdown",timer.Elapsed);}}public void StopRecording()=>StopRecordingAsync().GetAwaiter().GetResult();

    private void StartRecordingOnlyPump(ISampleProvider provider,WaveFormat format)
    {
        var cancellation=new CancellationTokenSource();_recordingOnlyPumpCancellation=cancellation;var samples=Math.Max(format.Channels,format.SampleRate*format.Channels*_requestedBuffer/1000);_recordingOnlyPump=Task.Run(async()=>{var buffer=new float[samples];while(!cancellation.IsCancellationRequested){provider.Read(buffer,0,buffer.Length);await Task.Delay(_requestedBuffer,cancellation.Token).ConfigureAwait(false);}},cancellation.Token);
    }

    private void OnCaptureData(object? sender,WaveInEventArgs e)
    {
        try{var buffer=_captureBuffer;if(buffer is null)return;if(buffer.BufferedBytes+e.BytesRecorded>buffer.BufferLength)Interlocked.Increment(ref _captureOverflows);buffer.AddSamples(e.Buffer,0,e.BytesRecorded);}
        catch(Exception ex){SchedulePrimaryFault(ex,true);}
    }
    private void OnCaptureStopped(object? sender,StoppedEventArgs e){if(e.Exception is not null)SchedulePrimaryFault(e.Exception,true);}
    private void OnPreviewPlaybackStopped(object? sender,StoppedEventArgs e){if(e.Exception is not null)SchedulePrimaryFault(e.Exception,true);else if(State==AudioEngineState.Running)SchedulePrimaryFault(new InvalidOperationException("The preview playback device stopped."),true);}
    private void OnEndpointUnavailable(string deviceId){if(deviceId==_activeInputId||deviceId==_activePreviewOutputId)SchedulePrimaryFault(new InvalidOperationException("An active preview device became unavailable."),true);}
    private void SchedulePrimaryFault(Exception error,bool deviceLost){if(State is AudioEngineState.Stopping or AudioEngineState.Stopped)return;if(Interlocked.Exchange(ref _faultScheduled,1)!=0)return;_deviceLost=deviceLost;RunBackground(()=>_lifecycle.FaultAsync(error,CleanupAsync));}

    private void FanOut(ReadOnlySpan<float> samples)
    {
        _meter.AddOutput(samples,Environment.TickCount64);
        if(_recording.State==RecordingState.Recording)_recording.TryEnqueue(samples);
    }
    private void OnMeterPublished(AudioMeterSnapshot snapshot){LevelsChanged?.Invoke(snapshot.InputPeak,snapshot.OutputPeak);MeterUpdated?.Invoke(snapshot);PublishDiagnostics();}
    private void PublishDiagnostics()=>DiagnosticsChanged?.Invoke(new(State,_activeInput,SampleRate,_requestedBuffer,_activeBuffer,EstimatedLatencyMs,_recording.DroppedBuffers,_deviceLost,_lastError,_captureOverflows>0?$"Capture buffer overflow was detected {Interlocked.Read(ref _captureOverflows)} time(s).":"No capture buffer overflow has been detected."));
    private static string Friendly(Exception ex)=>ex switch{UnauthorizedAccessException=>"The microphone is already in use or access was denied.",ArgumentException=>"The selected microphone does not support the requested audio format.",_=>"The microphone stopped or became unavailable. Refresh devices and try again."};
    private static LatencyProfile ClosestProfile(int ms)=>ms<=30?LatencyProfile.LowLatency:ms>=80?LatencyProfile.Safe:LatencyProfile.Balanced;

    [System.Diagnostics.Conditional("DEBUG")]
    private void PublishTiming(string operation,TimeSpan elapsed)=>DiagnosticTiming?.Invoke(operation,elapsed);
    private void RunBackground(Func<Task> operation)=>_ = Task.Run(async()=>{try{await operation().ConfigureAwait(false);}catch(Exception ex){TechnicalError?.Invoke(ex);}});
    private void CleanupComponent(Action cleanup){try{cleanup();}catch(Exception ex){TechnicalError?.Invoke(ex);}}

    public void Dispose(){try{Stop();}catch(Exception ex){TechnicalError?.Invoke(ex);}finally{_meterPublisher.Dispose();try{_recording.DisposeAsync().AsTask().GetAwaiter().GetResult();}catch(Exception ex){TechnicalError?.Invoke(ex);}try{_enumerator.UnregisterEndpointNotificationCallback(_notifications);}catch(Exception ex){TechnicalError?.Invoke(ex);}_enumerator.Dispose();GC.SuppressFinalize(this);}}

    private sealed class InputStageSampleProvider(ISampleProvider source,EffectChain chain,AudioMeterAccumulator meter):ISampleProvider
    {
        public WaveFormat WaveFormat=>source.WaveFormat;public int Read(float[] buffer,int offset,int count){var read=source.Read(buffer,offset,count);var span=buffer.AsSpan(offset,read);meter.AddInput(span,Environment.TickCount64);chain.Process(span,WaveFormat.SampleRate,WaveFormat.Channels);return read;}
    }
    private sealed class FanoutSampleProvider(ISampleProvider source,EffectChain chain,AudioEngine engine):ISampleProvider
    {
        private float _currentPitchFactor=float.NaN;public WaveFormat WaveFormat=>source.WaveFormat;
        public int Read(float[] buffer,int offset,int count){var target=engine.Pitch.IsEnabled?MathF.Pow(2,engine.Pitch.Semitones/12):1;if(float.IsNaN(_currentPitchFactor))_currentPitchFactor=target;else _currentPitchFactor+=(target-_currentPitchFactor)*.2f;engine._pitchProvider!.PitchFactor=_currentPitchFactor;var read=source.Read(buffer,offset,count);var span=buffer.AsSpan(offset,read);chain.Process(span,WaveFormat.SampleRate,WaveFormat.Channels);for(var i=0;i<span.Length;i++)span[i]=Math.Clamp(span[i],-1,1);engine.FanOut(span);return read;}
    }
    private sealed class EndpointNotificationClient(Action changed,Action<string> unavailable):IMMNotificationClient
    {
        public void OnDeviceStateChanged(string deviceId,DeviceState newState){if(newState!=DeviceState.Active)unavailable(deviceId);changed();}public void OnDeviceAdded(string pwstrDeviceId)=>changed();public void OnDeviceRemoved(string deviceId){unavailable(deviceId);changed();}public void OnDefaultDeviceChanged(DataFlow flow,Role role,string defaultDeviceId)=>changed();public void OnPropertyValueChanged(string pwstrDeviceId,PropertyKey key){}
    }
}
