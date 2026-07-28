namespace VoiceLab.Effects;

public sealed class NoiseGateEffect : IAudioEffect
{
    private float _envelope;
    private float _thresholdDb = -45, _attackMs = 5, _releaseMs = 100;
    public bool IsEnabled { get; set; }
    public float ThresholdDb { get => _thresholdDb; set => _thresholdDb = Math.Clamp(value, -80, 0); }
    public float AttackMs { get => _attackMs; set => _attackMs = Math.Clamp(value, .1f, 500); }
    public float ReleaseMs { get => _releaseMs; set => _releaseMs = Math.Clamp(value, 1, 3000); }
    public void Process(Span<float> samples, int sampleRate, int channels)
    {
        var threshold = MathF.Pow(10, ThresholdDb / 20); var attack = Coefficient(AttackMs, sampleRate); var release = Coefficient(ReleaseMs, sampleRate);
        for (var i = 0; i < samples.Length; i++) { var target = MathF.Abs(samples[i]) >= threshold ? 1f : 0f; var c = target > _envelope ? attack : release; _envelope += (target - _envelope) * c; samples[i] *= _envelope; }
    }
    private static float Coefficient(float ms, int rate) => 1 - MathF.Exp(-1 / (ms * .001f * rate));
    public void Reset() => _envelope = 0;
}

