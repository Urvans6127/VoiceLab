namespace VoiceLab.Audio;

public enum LatencyProfile { Safe, Balanced, LowLatency }
public enum AudioEngineMode { Recording, Preview }

public sealed record LatencyProfileSettings(LatencyProfile Profile, int RequestedBufferMilliseconds, string DisplayName, string Description)
{
    public static LatencyProfileSettings For(LatencyProfile profile) => profile switch
    {
        LatencyProfile.Safe => new(profile, 100, "Safe", "Highest compatibility for unstable drivers or slower systems."),
        LatencyProfile.LowLatency => new(profile, 25, "Low Latency", "Smaller buffers may crackle or drop out on some systems."),
        _ => new(LatencyProfile.Balanced, 50, "Balanced", "Conservative default suitable for most systems.")
    };
    public static LatencyProfile Parse(string? value) => value?.Replace(" ", "", StringComparison.OrdinalIgnoreCase) switch
    {
        "Safe" => LatencyProfile.Safe,
        "LowLatency" => LatencyProfile.LowLatency,
        _ => LatencyProfile.Balanced
    };
}

public sealed record AudioEngineStartOptions(string InputDeviceId,LatencyProfile Profile,int PreferredSampleRate=48000,AudioEngineMode Mode=AudioEngineMode.Recording,string? PreviewOutputDeviceId=null)
{
    public static AudioEngineStartOptions ForRecording(string inputDeviceId,LatencyProfile profile,int preferredSampleRate=48000)=>new(inputDeviceId,profile,preferredSampleRate);
    public static AudioEngineStartOptions ForPreview(string inputDeviceId,string outputDeviceId,LatencyProfile profile,int preferredSampleRate=48000)=>new(inputDeviceId,profile,preferredSampleRate,AudioEngineMode.Preview,outputDeviceId);
}
