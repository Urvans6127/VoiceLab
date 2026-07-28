using System.Buffers;
using System.Threading.Channels;
using NAudio.Wave;

namespace VoiceLab.Audio;

public enum RecordingState { Stopped, Recording, Paused, Stopping, Faulted }
public sealed record RecordingStatus(RecordingState State,string? FilePath,TimeSpan RecordedDuration,long DroppedBuffers,string? Error);

public sealed class RecordingPipeline : IAsyncDisposable
{
    public const int DefaultQueueCapacity=64;
    private readonly int _capacity;private readonly TimeSpan _writerDelay;private Channel<BufferLease>? _channel;private Task? _worker;private WaveFileWriter? _writer;private WaveFormat? _format;private long _samplesWritten,_dropped;private volatile RecordingState _state;private string? _path;private string? _error;
    public RecordingState State=>_state;public string? FilePath=>_path;public long DroppedBuffers=>Interlocked.Read(ref _dropped);public TimeSpan RecordedDuration=>_format is null?TimeSpan.Zero:TimeSpan.FromSeconds((double)Interlocked.Read(ref _samplesWritten)/_format.SampleRate/_format.Channels);
    public int QueueCapacity=>_capacity;public event Action<RecordingStatus>? StatusChanged;
    public RecordingPipeline(int capacity=DefaultQueueCapacity,TimeSpan? writerDelay=null){_capacity=Math.Max(2,capacity);_writerDelay=writerDelay??TimeSpan.Zero;}

    public Task<string> StartAsync(string directory,WaveFormat format,CancellationToken cancellationToken=default)
    {
        if(_state is RecordingState.Recording or RecordingState.Paused)return Task.FromResult(_path!);if(_state==RecordingState.Stopping||(_state==RecordingState.Faulted&&_worker is { IsCompleted:false }))throw new InvalidOperationException("The previous recording is still finalizing.");
        directory=NormalizeLocalDirectory(directory);
        Directory.CreateDirectory(directory);_format=format;_samplesWritten=0;_dropped=0;_error=null;
        FileStream? stream=null;var timestamp=DateTime.Now;for(var counter=0;stream is null;counter++){_path=CandidatePath(directory,timestamp,counter);try{stream=new FileStream(_path,FileMode.CreateNew,FileAccess.Write,FileShare.Read,4096,FileOptions.SequentialScan);}catch(IOException)when(File.Exists(_path)){}}
        try{_writer=new WaveFileWriter(stream,format);}catch{stream.Dispose();throw;}
        _channel=Channel.CreateBounded<BufferLease>(new BoundedChannelOptions(_capacity){SingleReader=true,SingleWriter=false,FullMode=BoundedChannelFullMode.Wait});_state=RecordingState.Recording;_worker=Task.Run(WriterLoopAsync);Publish();return Task.FromResult(_path!);
    }

    public bool TryEnqueue(ReadOnlySpan<float> samples)
    {
        if(_state!=RecordingState.Recording||_channel is null)return true;
        var rented=ArrayPool<float>.Shared.Rent(samples.Length);samples.CopyTo(rented);var lease=new BufferLease(rented,samples.Length);
        if(_channel.Writer.TryWrite(lease))return true;lease.Dispose();Interlocked.Increment(ref _dropped);return false;
    }
    public void Pause(){if(_state==RecordingState.Recording){_state=RecordingState.Paused;Publish();}}
    public void Resume(){if(_state==RecordingState.Paused){_state=RecordingState.Recording;Publish();}}
    public async Task StopAsync()
    {
        if(_state is RecordingState.Stopped or RecordingState.Stopping)return;_state=RecordingState.Stopping;Publish();_channel?.Writer.TryComplete();if(_worker is not null){var completed=await Task.WhenAny(_worker,Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);if(completed!=_worker){_error="The recording writer did not finalize within five seconds. The file may be incomplete.";_state=RecordingState.Faulted;Publish();return;}await _worker.ConfigureAwait(false);}_worker=null;_channel=null;if(_state!=RecordingState.Faulted)_state=RecordingState.Stopped;Publish();
    }
    private async Task WriterLoopAsync()
    {
        try{await foreach(var lease in _channel!.Reader.ReadAllAsync().ConfigureAwait(false)){using(lease){if(_writerDelay>TimeSpan.Zero)await Task.Delay(_writerDelay).ConfigureAwait(false);_writer!.WriteSamples(lease.Buffer,0,lease.Count);Interlocked.Add(ref _samplesWritten,lease.Count);}}}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){_error=ex.Message;_state=RecordingState.Faulted;while(_channel!.Reader.TryRead(out var lease))lease.Dispose();}
        finally{try{_writer?.Dispose();}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){_error=ex.Message;_state=RecordingState.Faulted;}_writer=null;Publish();}
    }
    private void Publish()=>StatusChanged?.Invoke(new(_state,_path,RecordedDuration,DroppedBuffers,_error));
    public static string CreateUniquePath(string directory,DateTime timestamp)
    {
        directory=NormalizeLocalDirectory(directory);
        for(var counter=0;;counter++){var path=CandidatePath(directory,timestamp,counter);if(!File.Exists(path))return path;}
    }
    public static string NormalizeLocalDirectory(string directory)
    {
        if(string.IsNullOrWhiteSpace(directory)||!Path.IsPathFullyQualified(directory))throw new ArgumentException("The recording directory must be a fully qualified local path.",nameof(directory));
        var full=Path.GetFullPath(directory.Trim());
        if(full.StartsWith(@"\\",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(Path.GetPathRoot(full)))throw new ArgumentException("Network and device paths are not supported for recordings.",nameof(directory));
        return full;
    }
    private static string CandidatePath(string directory,DateTime timestamp,int counter){var stem=$"VoiceLab-{timestamp:yyyyMMdd-HHmmss-fff}";var suffix=counter==0?"":$"-{counter}";return Path.Combine(directory,stem+suffix+".wav");}
    public async ValueTask DisposeAsync()=>await StopAsync().ConfigureAwait(false);
    private sealed class BufferLease(float[] buffer,int count):IDisposable{public float[] Buffer{get;}=buffer;public int Count{get;}=count;public void Dispose()=>ArrayPool<float>.Shared.Return(Buffer);}
}
