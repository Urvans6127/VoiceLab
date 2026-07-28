using System.Reflection;
namespace VoiceLab.App.ViewModels;
public sealed class AboutViewModel
{
    public string Version { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
}
