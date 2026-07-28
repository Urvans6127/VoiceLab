namespace VoiceLab.Effects;

public sealed class EffectChain
{
    private readonly IAudioEffect[] _effects;
    public EffectChain(params IAudioEffect[] effects) => _effects = effects;
    public IReadOnlyList<IAudioEffect> Effects => _effects;
    public void Process(Span<float> samples, int sampleRate, int channels)
    {
        foreach (var effect in _effects)
            if (effect.IsEnabled) effect.Process(samples, sampleRate, channels);
    }
    public void Reset() { foreach (var effect in _effects) effect.Reset(); }
}

