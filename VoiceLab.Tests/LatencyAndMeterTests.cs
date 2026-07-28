using VoiceLab.Audio;

namespace VoiceLab.Tests;

public sealed class LatencyAndMeterTests
{
    [Theory][InlineData(LatencyProfile.Safe,100)][InlineData(LatencyProfile.Balanced,50)][InlineData(LatencyProfile.LowLatency,25)]public void ProfilesHaveExplicitConservativeValues(LatencyProfile profile,int expected)=>Assert.Equal(expected,LatencyProfileSettings.For(profile).RequestedBufferMilliseconds);
    [Fact]public void BalancedIsDefault()=>Assert.Equal(LatencyProfile.Balanced,LatencyProfileSettings.Parse(null));
    [Fact]public void MeterCalculatesPeakAndRms(){var meter=new AudioMeterAccumulator();meter.AddInput([.5f,-1f],100);var result=meter.TakeSnapshot(100);Assert.Equal(1,result.InputPeak);Assert.InRange(result.InputRms,.79f,.8f);}
    [Fact]public void ClippingHoldsThenReleases(){var meter=new AudioMeterAccumulator();meter.AddOutput([.99f],100);Assert.True(meter.TakeSnapshot(200).OutputClipping);Assert.False(meter.TakeSnapshot(1700).OutputClipping);}
    [Fact]public async Task PublisherIsThrottledAndStops(){var meter=new AudioMeterAccumulator();using var publisher=new ThrottledMeterPublisher(meter);var count=0;publisher.Published+=_=>Interlocked.Increment(ref count);publisher.Start();for(var i=0;i<1000;i++)meter.AddInput([.1f],Environment.TickCount64);await Task.Delay(150);publisher.Stop();var stopped=count;await Task.Delay(100);Assert.InRange(stopped,2,6);Assert.Equal(stopped,count);}
}
