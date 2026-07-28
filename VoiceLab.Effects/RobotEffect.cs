namespace VoiceLab.Effects;

public sealed class RobotEffect : IAudioEffect
{
    private double _phase; private float _frequency = 70, _mix = .7f;
    public bool IsEnabled { get; set; }
    public float CarrierFrequency { get => _frequency; set => _frequency = Math.Clamp(value, 20, 1000); }
    public float Mix { get => _mix; set => _mix = Math.Clamp(value, 0, 1); }
    public void Process(Span<float> samples, int sampleRate, int channels)
    { var step = 2 * Math.PI * CarrierFrequency / sampleRate / channels; var mix = Mix; for (var i=0;i<samples.Length;i++) { var wet = samples[i] * (float)Math.Sin(_phase); samples[i] = samples[i] * (1-mix) + wet*mix; _phase = (_phase+step)%(2*Math.PI); } }
    public void Reset() => _phase = 0;
}

