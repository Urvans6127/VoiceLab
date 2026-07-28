using VoiceLab.Infrastructure;

namespace VoiceLab.Tests;

public sealed class FileLoggerTests
{
    [Fact]
    public void DuplicateErrorsAreCoalescedWithoutLosingStackTrace()
    {
        WithDirectory(directory=>
        {
            var logger=new FileLogger(directory);
            logger.Log("Audio failed",CaptureException());
            logger.Log("Audio failed",CaptureException());
            var text=File.ReadAllText(Directory.GetFiles(directory,"*.log").Single());
            Assert.Equal(1,Count(text,"Audio failed"));
            Assert.Contains(nameof(CaptureException),text);
        });
    }

    [Fact]
    public void OldLogFilesArePrunedToRetentionLimit()
    {
        WithDirectory(directory=>
        {
            for(var index=0;index<9;index++)
            {
                var path=Path.Combine(directory,$"old-{index}.log");
                File.WriteAllText(path,"old");
                File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddDays(-index-1));
            }
            new FileLogger(directory).Log("current");
            Assert.Equal(7,Directory.GetFiles(directory,"*.log").Length);
        });
    }

    private static Exception CaptureException(){try{throw new InvalidOperationException("same failure");}catch(Exception ex){return ex;}}
    private static int Count(string value,string needle)=>(value.Length-value.Replace(needle,string.Empty).Length)/needle.Length;
    private static void WithDirectory(Action<string> test){var directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);try{test(directory);}finally{Directory.Delete(directory,true);}}
}
