namespace VoiceLab.Audio;

public sealed record AudioDevice(string Id,string Name)
{
    public string DisplayName=>Name;
}
