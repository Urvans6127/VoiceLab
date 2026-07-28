namespace VoiceLab.Effects;

/// <summary>
/// Lightweight tonal shaping for live voice processing. VoiceDepth changes the
/// low/high balance to suggest a deeper or lighter voice; it is not a formant shifter.
/// </summary>
public sealed class ToneShapingEffect : IAudioEffect
{
    private float[] _lowState = [];
    private float[] _presenceState = [];
    private float _voiceDepth;
    private float _brightness;
    private float _bassDb;
    private float _trebleDb;
    private float _mix;
    private float _saturation;
    private float _currentBassGain = float.NaN;
    private float _currentTrebleGain = float.NaN;
    private float _currentMix = float.NaN;
    private float _currentSaturation = float.NaN;

    public bool IsEnabled { get; set; } = true;
    public float VoiceDepth { get => Volatile.Read(ref _voiceDepth); set => Volatile.Write(ref _voiceDepth, Math.Clamp(value, -1, 1)); }
    public float Brightness { get => Volatile.Read(ref _brightness); set => Volatile.Write(ref _brightness, Math.Clamp(value, -1, 1)); }
    public float BassDb { get => Volatile.Read(ref _bassDb); set => Volatile.Write(ref _bassDb, Math.Clamp(value, -12, 12)); }
    public float TrebleDb { get => Volatile.Read(ref _trebleDb); set => Volatile.Write(ref _trebleDb, Math.Clamp(value, -12, 12)); }
    public float Mix { get => Volatile.Read(ref _mix); set => Volatile.Write(ref _mix, Math.Clamp(value, 0, 1)); }
    public float Saturation { get => Volatile.Read(ref _saturation); set => Volatile.Write(ref _saturation, Math.Clamp(value, 0, 1)); }

    public void Process(Span<float> samples, int sampleRate, int channels)
    {
        if (samples.IsEmpty || sampleRate <= 0 || channels <= 0) return;
        EnsureChannels(channels);

        var depth = VoiceDepth;
        var brightness = Brightness;
        var targetBassGain = DbToGain(BassDb + depth * 6);
        var targetTrebleGain = DbToGain(TrebleDb + brightness * 8 - depth * 3);
        var targetMix = Mix;
        var targetSaturation = Saturation;
        if (float.IsNaN(_currentMix))
        {
            _currentBassGain = targetBassGain;
            _currentTrebleGain = targetTrebleGain;
            _currentMix = targetMix;
            _currentSaturation = targetSaturation;
        }
        var smoothing = 1 - MathF.Exp(-1 / (.02f * sampleRate));
        var lowAlpha = OnePoleAlpha(180, sampleRate);
        var presenceAlpha = OnePoleAlpha(2800, sampleRate);

        for (var i = 0; i < samples.Length; i++)
        {
            var channel = i % channels;
            var dry = samples[i];
            _currentBassGain += (targetBassGain - _currentBassGain) * smoothing;
            _currentTrebleGain += (targetTrebleGain - _currentTrebleGain) * smoothing;
            _currentMix += (targetMix - _currentMix) * smoothing;
            _currentSaturation += (targetSaturation - _currentSaturation) * smoothing;
            _lowState[channel] += lowAlpha * (dry - _lowState[channel]);
            _presenceState[channel] += presenceAlpha * (dry - _presenceState[channel]);
            var low = _lowState[channel];
            var high = dry - _presenceState[channel];
            var middle = dry - low - high;
            var shaped = low * _currentBassGain + middle + high * _currentTrebleGain;
            if (_currentSaturation > .0001f)
            {
                var drive = 1 + _currentSaturation * 3;
                shaped = MathF.Tanh(shaped * drive) / MathF.Tanh(drive);
            }
            samples[i] = Math.Clamp(dry + (shaped - dry) * _currentMix, -1, 1);
        }
    }

    public void Reset()
    {
        Array.Clear(_lowState);
        Array.Clear(_presenceState);
        _currentBassGain = float.NaN;
        _currentTrebleGain = float.NaN;
        _currentMix = float.NaN;
        _currentSaturation = float.NaN;
    }

    private void EnsureChannels(int channels)
    {
        if (_lowState.Length == channels) return;
        _lowState = new float[channels];
        _presenceState = new float[channels];
    }

    private static float DbToGain(float db) => MathF.Pow(10, db / 20);
    private static float OnePoleAlpha(float cutoff, int sampleRate) => 1 - MathF.Exp(-2 * MathF.PI * cutoff / sampleRate);
}
