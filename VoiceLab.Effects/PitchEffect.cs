namespace VoiceLab.Effects;

public sealed class PitchEffect : IAudioEffect
{
    private float _semitones;
    public bool IsEnabled { get; set; }
    public float Semitones { get=>_semitones; set=>_semitones=Math.Clamp(value,-12,12); }
    public void Process(Span<float> samples,int sampleRate,int channels)
    {
        // SMB-style phase-vocoder pitch shifting is performed by NAudio's managed SmbPitchShiftingSampleProvider in the audio pipeline.
        // This marker effect keeps ordering/preset semantics without a second pitch pass.
    }
    public void Reset()=>Semitones=0;
}
