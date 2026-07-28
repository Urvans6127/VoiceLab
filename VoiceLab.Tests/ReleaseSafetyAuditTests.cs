using System.Text.RegularExpressions;

namespace VoiceLab.Tests;

public sealed class ReleaseSafetyAuditTests
{
    private static string Root=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));

    [Fact]
    public void ProductionCodeContainsNoNetworkClientApis()
    {
        string[] projects=["VoiceLab.App","VoiceLab.Audio","VoiceLab.Effects","VoiceLab.Infrastructure"];
        string[] forbidden=["HttpClient","WebClient","HttpWebRequest","TcpClient","UdpClient","WebSocket","System.Net.Sockets","Dns.GetHost","DownloadFile","DownloadString"];
        foreach(var path in projects.SelectMany(project=>Directory.GetFiles(Path.Combine(Root,project),"*.cs",SearchOption.AllDirectories)).Where(path=>!path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",StringComparison.OrdinalIgnoreCase)))
        {
            var source=File.ReadAllText(path);
            foreach(var api in forbidden)Assert.DoesNotContain(api,source,StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GitIgnoreCoversReleaseSensitiveArtifacts()
    {
        var ignore=File.ReadAllText(Path.Combine(Root,".gitignore"));
        foreach(var pattern in new[]{"bin/","obj/","publish/","*.pdb","*.wav","[Ll]ogs/","settings.json","presets.json","*.dmp",".env","*.zip"})
            Assert.Contains(pattern,ignore,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EveryXamlResourceReferenceResolvesWithinApplicationResources()
    {
        var files=Directory.GetFiles(Path.Combine(Root,"VoiceLab.App"),"*.xaml",SearchOption.AllDirectories)
            .Where(path=>!path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var all=string.Join(Environment.NewLine,files.Select(File.ReadAllText));
        var definitions=Regex.Matches(all,"x:Key=\"([^\"]+)\"").Select(match=>match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var references=Regex.Matches(all,@"\{(?:StaticResource|DynamicResource)\s+([^},]+)")
            .Select(match=>match.Groups[1].Value.Trim())
            .Where(key=>!key.StartsWith("{x:Type",StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal);
        Assert.DoesNotContain(references,key=>!definitions.Contains(key));
    }
}
