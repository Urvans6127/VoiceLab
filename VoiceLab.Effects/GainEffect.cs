namespace VoiceLab.Effects;

public sealed class GainEffect : IAudioEffect
{
    private float _gain = 1;
    private float _currentGain = float.NaN;
    public bool IsEnabled { get; set; } = true;
    public float Gain { get => Volatile.Read(ref _gain); set => Volatile.Write(ref _gain, Math.Clamp(value, 0, 4)); }
    public void Process(Span<float> samples, int sampleRate, int channels)
    {
        var target = Gain;
        if (float.IsNaN(_currentGain)) _currentGain = target;
        var smoothing = sampleRate > 0 ? 1 - MathF.Exp(-1 / (.015f * sampleRate)) : 1;
        for (var i = 0; i < samples.Length; i++)
        {
            _currentGain += (target - _currentGain) * smoothing;
            samples[i] = Math.Clamp(samples[i] * _currentGain, -1, 1);
        }
    }
    public void Reset() => _currentGain = float.NaN;
}
