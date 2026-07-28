namespace VoiceLab.Effects;

public sealed class ReverbEffect : IAudioEffect
{
    private float[][] _lines=[]; private int[] _positions=[]; private int _rate,_channels; private float _roomSize=.5f,_mix=.2f;
    public bool IsEnabled { get; set; }
    public float RoomSize { get=>_roomSize; set=>_roomSize=Math.Clamp(value,0,1); }
    public float Mix { get=>_mix; set=>_mix=Math.Clamp(value,0,.8f); }
    public void Process(Span<float> samples,int sampleRate,int channels){Ensure(sampleRate,channels);for(var i=0;i<samples.Length;i++){var dry=samples[i];float sum=0;for(var j=0;j<_lines.Length;j++){var p=_positions[j];var delayed=_lines[j][p];_lines[j][p]=Math.Clamp(dry+delayed*(.55f+RoomSize*.35f),-1,1);_positions[j]=(p+1)%_lines[j].Length;sum+=delayed;}samples[i]=Math.Clamp(dry*(1-Mix)+sum/_lines.Length*Mix,-1,1);}}
    private void Ensure(int rate,int channels){if(_rate==rate&&_channels==channels)return;int[] ms=[29,37,43,53];_lines=ms.Select(x=>new float[Math.Max(1,rate*channels*x/1000)]).ToArray();_positions=new int[ms.Length];_rate=rate;_channels=channels;}
    public void Reset(){foreach(var line in _lines)Array.Clear(line);Array.Clear(_positions);}
}
