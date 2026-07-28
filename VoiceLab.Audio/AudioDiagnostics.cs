namespace VoiceLab.Audio;

public sealed record AudioMeterSnapshot(float InputPeak, float InputRms, float OutputPeak, float OutputRms, bool InputClipping, bool OutputClipping);

public sealed record AudioDiagnostics(
    AudioEngineState State,
    string? ActiveInput,
    int ActiveSampleRate,
    int RequestedBufferMilliseconds,
    double ActiveBufferMilliseconds,
    double EstimatedBufferingLatencyMilliseconds,
    long RecordingDroppedBuffers,
    bool DeviceLost,
    string? LastRecoverableError,
    string OverflowStatus = "Capture overflow counters are reported when available.");
