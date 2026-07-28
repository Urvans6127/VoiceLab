using VoiceLab.Infrastructure;

namespace VoiceLab.Tests;

public sealed class StorageCompatibilityTests
{
    [Fact]
    public void RenamedApplicationKeepsAllLegacyUserDataPaths()
    {
        var legacyLocal = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LocalVoiceChanger");
        var legacyRoaming = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LocalVoiceChanger");

        Assert.Equal(Path.Combine(legacyLocal, "settings.json"), VoiceLabStoragePaths.SettingsFile);
        Assert.Equal(Path.Combine(legacyLocal, "Recordings"), VoiceLabStoragePaths.RecordingsDirectory);
        Assert.Equal(Path.Combine(legacyLocal, "logs"), VoiceLabStoragePaths.LogsDirectory);
        Assert.Equal(Path.Combine(legacyRoaming, "presets.json"), VoiceLabStoragePaths.PresetsFile);
        Assert.Equal(VoiceLabStoragePaths.SettingsFile, new JsonSettingsStore().Path);
        Assert.Equal(VoiceLabStoragePaths.RecordingsDirectory, new ApplicationSettings().RecordingFolder);
    }
}
