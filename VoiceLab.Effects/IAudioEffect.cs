namespace VoiceLab.Effects;

public interface IAudioEffect
{
    bool IsEnabled { get; set; }
    void Process(Span<float> samples, int sampleRate, int channels);
    void Reset();
}

