using System.Diagnostics;
using System.IO;
namespace VoiceLab.App.Services;
public sealed class FolderLauncher
{
    public void Open(string path)
    {
        if(!Path.IsPathFullyQualified(path)||path.StartsWith(@"\\",StringComparison.Ordinal)||path.StartsWith(@"\\?\",StringComparison.Ordinal))throw new IOException("Only a local recording folder can be opened.");
        var full=Path.GetFullPath(path);if(!Directory.Exists(full))throw new DirectoryNotFoundException("The recording folder does not exist.");
        var info=new ProcessStartInfo("explorer.exe"){UseShellExecute=false};info.ArgumentList.Add(full);Process.Start(info);
    }
}
