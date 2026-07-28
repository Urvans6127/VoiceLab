namespace VoiceLab.Effects;

public sealed class EchoEffect : IAudioEffect
{
    private float[] _buffer = Array.Empty<float>(); private int _position, _configuredRate, _configuredChannels; private float _delayMs=250, _feedback=.3f, _mix=.3f;
    public bool IsEnabled { get; set; }
    public float DelayMs { get => _delayMs; set { _delayMs=Math.Clamp(value,10,1500); _configuredRate=0; } }
    public float Feedback { get => _feedback; set => _feedback=Math.Clamp(value,0,.9f); }
    public float Mix { get => _mix; set => _mix=Math.Clamp(value,0,1); }
    public void Process(Span<float> samples, int sampleRate, int channels)
    {
        EnsureBuffer(sampleRate,channels); var feedback=Feedback; var mix=Mix;
        for(var i=0;i<samples.Length;i++){var dry=samples[i];var delayed=_buffer[_position];_buffer[_position]=Math.Clamp(dry+delayed*feedback,-1,1);samples[i]=Math.Clamp(dry*(1-mix)+delayed*mix,-1,1);_position=(_position+1)%_buffer.Length;}
    }
    private void EnsureBuffer(int rate,int channels){if(rate==_configuredRate&&channels==_configuredChannels)return;_buffer=new float[Math.Max(1,(int)(rate*channels*DelayMs/1000))];_position=0;_configuredRate=rate;_configuredChannels=channels;}
    public void Reset(){Array.Clear(_buffer);_position=0;}
}

