using VoiceLab.Audio;

namespace VoiceLab.Tests;

public sealed class LivePreviewRegressionTests
{
    private static string Root=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
    private static string Read(params string[] parts)=>File.ReadAllText(Path.Combine([Root,..parts]));

    [Fact]
    public void PreviewOptionsRequireAnExplicitLocalPlaybackDevice()
    {
        var options=AudioEngineStartOptions.ForPreview("microphone","headphones",LatencyProfile.LowLatency,48000);
        Assert.Equal(AudioEngineMode.Preview,options.Mode);
        Assert.Equal("microphone",options.InputDeviceId);
        Assert.Equal("headphones",options.PreviewOutputDeviceId);
        Assert.Equal(48000,options.PreferredSampleRate);
    }

    [Fact]
    public void PreviewUsesTheSharedDspProviderWithoutStartingARecording()
    {
        var source=Read("VoiceLab.Audio","AudioEngine.cs");
        Assert.Contains("var fanout=new FanoutSampleProvider(pitch,new(ToneShape,Robot,Echo,Reverb,OutputGain),this)",source);
        Assert.Contains("previewOutput.Init(fanout)",source);
        Assert.Equal(1,source.Split("_recording.StartAsync",StringSplitOptions.None).Length-1);
        Assert.Contains("if(_recording.State==RecordingState.Recording)_recording.TryEnqueue(samples)",source);
    }

    [Fact]
    public void PreviewEnumeratesOrdinaryWindowsPlaybackEndpointsWithoutVirtualDeviceHeuristics()
    {
        var source=Read("VoiceLab.Audio","AudioEngine.cs");
        Assert.Contains("GetPreviewOutputDevices",source);
        Assert.Contains("DataFlow.Render,DeviceState.Active",source);
        Assert.DoesNotContain("AudioDeviceHeuristics",source);
        Assert.DoesNotContain("GetDefaultAudioEndpoint",source);
    }

    [Fact]
    public async Task PreviewLifecycleStopsDisposesAndAllowsRecordingSessionAfterward()
    {
        var lifecycle=new EngineLifecycleCoordinator();
        var previewCreated=0;
        var previewDisposed=0;
        Assert.True(await lifecycle.StartAsync(_=>{previewCreated++;return Task.CompletedTask;},()=>Task.CompletedTask));
        await lifecycle.StopAsync(()=>{previewDisposed++;return Task.CompletedTask;});
        Assert.Equal(AudioEngineState.Stopped,lifecycle.State);
        Assert.Equal(1,previewCreated);
        Assert.Equal(1,previewDisposed);
        Assert.True(await lifecycle.StartAsync(_=>Task.CompletedTask,()=>Task.CompletedTask));
        Assert.Equal(AudioEngineState.Running,lifecycle.State);
    }

    [Fact]
    public async Task RepeatedPreviewStartCannotCreateDuplicateSession()
    {
        var lifecycle=new EngineLifecycleCoordinator();
        var instances=0;
        Assert.True(await lifecycle.StartAsync(_=>{instances++;return Task.CompletedTask;},()=>Task.CompletedTask));
        Assert.False(await lifecycle.StartAsync(_=>{instances++;return Task.CompletedTask;},()=>Task.CompletedTask));
        Assert.Equal(1,instances);
        await lifecycle.StopAsync(()=>Task.CompletedTask);
    }

    [Fact]
    public async Task PreviewDeviceLossCleansUpAndCanReturnToIdle()
    {
        var lifecycle=new EngineLifecycleCoordinator();
        var disposed=0;
        await lifecycle.StartAsync(_=>Task.CompletedTask,()=>Task.CompletedTask);
        await lifecycle.FaultAsync(new IOException("preview device removed"),()=>{disposed++;return Task.CompletedTask;});
        Assert.Equal(AudioEngineState.Faulted,lifecycle.State);
        Assert.Equal(1,disposed);
        await lifecycle.StopAsync(()=>Task.CompletedTask);
        Assert.Equal(AudioEngineState.Stopped,lifecycle.State);
    }

    [Fact]
    public void ApplicationShutdownStopsTheSharedAudioEngine()
    {
        var source=Read("VoiceLab.App","App.xaml.cs");
        Assert.Contains("GetRequiredService<AudioEngine>().StopAsync()",source);
        Assert.Contains("GetRequiredService<AudioEngine>().StopAsync().GetAwaiter().GetResult()",source);
    }
}
