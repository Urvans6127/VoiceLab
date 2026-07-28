using VoiceLab.Effects;
namespace VoiceLab.Tests;
public sealed class EffectTests
{
 [Fact]public void GainScalesAndClamps(){var e=new GainEffect{Gain=2};float[] s=[.25f,.75f,-.75f];e.Process(s,48000,1);Assert.Equal([.5f,1,-1],s);}
 [Fact]public void GainParameterIsValidated(){var e=new GainEffect{Gain=99};Assert.Equal(4,e.Gain);e.Gain=-2;Assert.Equal(0,e.Gain);}
 [Fact]public void GainChangesAreSmoothedAfterInitialization(){var e=new GainEffect{Gain=1};float[] initial=[1];e.Process(initial,48000,1);e.Gain=0;var transition=Enumerable.Repeat(1f,480).ToArray();e.Process(transition,48000,1);Assert.True(transition[0]>.9f);Assert.True(transition[^1]<transition[0]);Assert.True(transition[^1]>0);}
 [Fact]public void ClosedNoiseGateAttenuatesQuietSignal(){var e=new NoiseGateEffect{IsEnabled=true,ThresholdDb=-20,AttackMs=.1f,ReleaseMs=1};var s=Enumerable.Repeat(.001f,480).ToArray();e.Process(s,48000,1);Assert.All(s,x=>Assert.True(Math.Abs(x)<.001f));}
 [Fact]public void EchoReturnsDelayedSample(){var e=new EchoEffect{IsEnabled=true,DelayMs=10,Mix=1,Feedback=0};var s=new float[481];s[0]=1;e.Process(s,48000,1);Assert.Equal(0,s[0]);Assert.Equal(1,s[480]);}
 [Fact]public void EchoFeedbackCannotBecomeUnstable(){var e=new EchoEffect{Feedback=5};Assert.Equal(.9f,e.Feedback);}
 [Fact]public void ToneShapingParametersAreBounded(){var e=new ToneShapingEffect{VoiceDepth=5,Brightness=-5,BassDb=20,TrebleDb=-20,Mix=2,Saturation=3};Assert.Equal(1,e.VoiceDepth);Assert.Equal(-1,e.Brightness);Assert.Equal(12,e.BassDb);Assert.Equal(-12,e.TrebleDb);Assert.Equal(1,e.Mix);Assert.Equal(1,e.Saturation);}
 [Fact]public void ToneShapingProcessesLiveSignalWithoutClipping(){var e=new ToneShapingEffect{VoiceDepth=.8f,Brightness=-.3f,BassDb=6,TrebleDb=-4,Mix=.9f,Saturation=.2f};var s=Enumerable.Repeat(.75f,1024).ToArray();e.Process(s,48000,1);Assert.All(s,x=>Assert.True(float.IsFinite(x)&&Math.Abs(x)<=1));Assert.NotEqual(.75f,s[^1]);}
 [Fact]public void ReverbReinitializesWhenChannelCountChanges(){var e=new ReverbEffect{IsEnabled=true,Mix=.5f};var mono=new float[480];mono[0]=1;e.Process(mono,48000,1);var stereo=new float[960];stereo[0]=1;e.Process(stereo,48000,2);Assert.All(stereo,x=>Assert.True(float.IsFinite(x)&&Math.Abs(x)<=1));}
 [Fact]public void ChainPreservesOrdering(){var seen=new List<int>();var chain=new EffectChain(new Marker(1,seen),new Marker(2,seen));chain.Process(new float[1],48000,1);Assert.Equal([1,2],seen);}
 sealed class Marker(int id,List<int> seen):IAudioEffect{public bool IsEnabled{get;set;}=true;public void Process(Span<float>s,int r,int c)=>seen.Add(id);public void Reset(){}}
}
