using VoiceLab.Audio;

namespace VoiceLab.Tests;

public sealed class RecordingModeRegressionTests
{
    [Fact] public void RecordingOptionsContainOnlyCaptureConfiguration()
    {
        var options=AudioEngineStartOptions.ForRecording("microphone",LatencyProfile.Balanced,44100);
        Assert.Equal("microphone",options.InputDeviceId);
        Assert.Equal(LatencyProfile.Balanced,options.Profile);
        Assert.Equal(44100,options.PreferredSampleRate);
        Assert.Equal(AudioEngineMode.Recording,options.Mode);
        Assert.Null(options.PreviewOutputDeviceId);
    }

    [Fact] public void PlaybackPathIsRestrictedToExplicitPreviewMode()
    {
        var path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","VoiceLab.Audio","AudioEngine.cs"));
        var source=File.ReadAllText(path);
        Assert.Contains("WasapiCapture",source);
        Assert.Contains("StartRecording",source);
        Assert.Contains("WasapiOut",source);
        Assert.Contains("if(options.Mode==AudioEngineMode.Preview)",source);
        Assert.Contains("if(previewOutput is not null)previewOutput.Play();else StartRecordingOnlyPump",source);
        Assert.Contains("_mode!=AudioEngineMode.Recording",source);
        Assert.DoesNotContain("GetDefaultAudioEndpoint",source);
    }

    [Fact] public void ComboBoxThemeDefinesExplicitReadableSemanticBrushes()
    {
        var path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","VoiceLab.App","Resources","Theme.xaml"));
        var theme=File.ReadAllText(path);
        foreach(var resource in new[]{"ComboBoxBackgroundBrush","ComboBoxForegroundBrush","ComboBoxDisabledBackgroundBrush","ComboBoxDisabledForegroundBrush","ComboBoxItemBackgroundBrush","ComboBoxItemForegroundBrush","ComboBoxItemHoverBackgroundBrush","ComboBoxItemHoverForegroundBrush","ComboBoxItemSelectedBackgroundBrush","ComboBoxItemSelectedForegroundBrush"})Assert.Contains($"x:Key=\"{resource}\"",theme);
        Assert.Contains("Value=\"{StaticResource ComboBoxForegroundBrush}\"",theme);
        Assert.Contains("Value=\"{StaticResource ComboBoxDisabledForegroundBrush}\"",theme);
    }
}
