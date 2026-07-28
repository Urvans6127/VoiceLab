namespace VoiceLab.Audio;

public sealed class AudioMeterAccumulator
{
    private int _inputPeakBits, _outputPeakBits;
    private double _inputSquares, _outputSquares;
    private long _inputCount, _outputCount, _inputClipUntil, _outputClipUntil;

    public void AddInput(ReadOnlySpan<float> samples, long nowMilliseconds) => Add(samples, ref _inputPeakBits, ref _inputSquares, ref _inputCount, ref _inputClipUntil, nowMilliseconds);
    public void AddOutput(ReadOnlySpan<float> samples, long nowMilliseconds) => Add(samples, ref _outputPeakBits, ref _outputSquares, ref _outputCount, ref _outputClipUntil, nowMilliseconds);

    public AudioMeterSnapshot TakeSnapshot(long nowMilliseconds)
    {
        var inputCount=Interlocked.Exchange(ref _inputCount,0);var outputCount=Interlocked.Exchange(ref _outputCount,0);
        var inputSquares=Interlocked.Exchange(ref _inputSquares,0);var outputSquares=Interlocked.Exchange(ref _outputSquares,0);
        return new(BitConverter.Int32BitsToSingle(Interlocked.Exchange(ref _inputPeakBits,0)),inputCount==0?0:(float)Math.Sqrt(inputSquares/inputCount),BitConverter.Int32BitsToSingle(Interlocked.Exchange(ref _outputPeakBits,0)),outputCount==0?0:(float)Math.Sqrt(outputSquares/outputCount),Volatile.Read(ref _inputClipUntil)>nowMilliseconds,Volatile.Read(ref _outputClipUntil)>nowMilliseconds);
    }

    private static void Add(ReadOnlySpan<float> samples,ref int peakBits,ref double squares,ref long count,ref long clipUntil,long now)
    {
        float peak=0;double sum=0;foreach(var sample in samples){var absolute=Math.Abs(sample);if(absolute>peak)peak=absolute;sum+=sample*sample;}
        AtomicMax(ref peakBits,peak);AddDouble(ref squares,sum);Interlocked.Add(ref count,samples.Length);if(peak>=.98f)Interlocked.Exchange(ref clipUntil,now+1500);
    }
    private static void AtomicMax(ref int target,float value){var current=BitConverter.Int32BitsToSingle(Volatile.Read(ref target));while(value>current){var prior=Interlocked.CompareExchange(ref target,BitConverter.SingleToInt32Bits(value),BitConverter.SingleToInt32Bits(current));if(prior==BitConverter.SingleToInt32Bits(current))break;current=BitConverter.Int32BitsToSingle(prior);}}
    private static void AddDouble(ref double target,double value){double current;do{current=Volatile.Read(ref target);}while(Interlocked.CompareExchange(ref target,current+value,current)!=current);}
}

public sealed class ThrottledMeterPublisher : IDisposable
{
    private readonly AudioMeterAccumulator _accumulator;private Timer? _timer;private int _generation;
    public event Action<AudioMeterSnapshot>? Published;
    public ThrottledMeterPublisher(AudioMeterAccumulator accumulator)=>_accumulator=accumulator;
    public void Start(){Stop();var generation=Volatile.Read(ref _generation);_timer=new Timer(_=>{if(generation==Volatile.Read(ref _generation))Published?.Invoke(_accumulator.TakeSnapshot(Environment.TickCount64));},null,40,40);}
    public void Stop(){Interlocked.Increment(ref _generation);Interlocked.Exchange(ref _timer,null)?.Dispose();}
    public void Dispose()=>Stop();
}
