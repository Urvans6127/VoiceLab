namespace VoiceLab.Infrastructure;

public static class VoiceLabStoragePaths
{
    // Preserve the original product directory so renamed builds load existing user data in place.
    public const string CompatibilityDirectoryName = "LocalVoiceChanger";

    public static string LocalApplicationDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CompatibilityDirectoryName);

    public static string RoamingApplicationDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), CompatibilityDirectoryName);

    public static string SettingsFile => Path.Combine(LocalApplicationDataDirectory, "settings.json");
    public static string RecordingsDirectory => Path.Combine(LocalApplicationDataDirectory, "Recordings");
    public static string LogsDirectory => Path.Combine(LocalApplicationDataDirectory, "logs");
    public static string PresetsFile => Path.Combine(RoamingApplicationDataDirectory, "presets.json");
}
