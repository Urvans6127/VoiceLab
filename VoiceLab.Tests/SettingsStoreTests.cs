using VoiceLab.Infrastructure;

namespace VoiceLab.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task OversizedSettingsFileReturnsDefaultsAndIsQuarantined()
    {
        await WithStore(async (store, path) =>
        {
            using (var stream = new FileStream(path, FileMode.CreateNew))
                stream.SetLength(1024L * 1024 + 1);
            var loaded = await store.LoadAsync();
            Assert.Equal(ApplicationSettings.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.NotEmpty(Directory.GetFiles(Path.GetDirectoryName(path)!, "settings.corrupt-*.json"));
        });
    }

    [Fact]
    public async Task MissingFileReturnsSafeDefaults() => await WithStore(async (store, _) =>
    {
        var settings=await store.LoadAsync();
        Assert.Equal("Recording",settings.LastSelectedPage);
        Assert.Equal("en",settings.Language);
    });

    [Fact]
    public async Task RecordingSettingsRoundTrip() => await WithStore(async (store, _) =>
    {
        var expected=new ApplicationSettings{LastInputDeviceId="mic",LastPreviewOutputDeviceId="headphones",LastSelectedPreset="Natural Male",LastSelectedPage="Presets",RecordingFolder="C:\\Recordings",WindowWidth=1280,WindowHeight=720,LatencyProfile="Safe",PreferredSampleRate=44100,Language="tr"};
        await store.SaveAsync(expected);
        var actual=await store.LoadAsync();
        Assert.Equal(expected.LastInputDeviceId,actual.LastInputDeviceId);
        Assert.Equal(expected.LastPreviewOutputDeviceId,actual.LastPreviewOutputDeviceId);
        Assert.Equal(expected.LastSelectedPreset,actual.LastSelectedPreset);
        Assert.Equal("Presets",actual.LastSelectedPage);
        Assert.Equal("C:\\Recordings",actual.RecordingFolder);
        Assert.Equal(1280,actual.WindowWidth);
        Assert.Equal("Safe",actual.LatencyProfile);
        Assert.Equal(44100,actual.PreferredSampleRate);
        Assert.Equal("tr",actual.Language);
    });

    [Fact]
    public async Task AtomicSaveLeavesNoTemporaryFile() => await WithStore(async (store,path) =>
    {
        await store.SaveAsync(new());
        Assert.True(File.Exists(path));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!,"*.tmp"));
    });

    [Fact]
    public async Task CorruptedFileIsQuarantined() => await WithStore(async (store,path) =>
    {
        await File.WriteAllTextAsync(path,"not json");
        var loaded=await store.LoadAsync();
        Assert.Equal(1120,loaded.WindowWidth);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!,"settings.corrupt-*.json"));
        Assert.False(File.Exists(path));
    });

    [Fact]
    public async Task InvalidGeometryFallsBack() => await WithStore(async (store,_) =>
    {
        await store.SaveAsync(new ApplicationSettings{WindowWidth=double.NaN,WindowHeight=-5,WindowLeft=double.PositiveInfinity});
        var loaded=await store.LoadAsync();
        Assert.Equal(1120,loaded.WindowWidth);
        Assert.Equal(860,loaded.WindowHeight);
        Assert.Null(loaded.WindowLeft);
    });

    [Fact]
    public void UnavailableSavedDeviceFallsBackSafely()
    {
        Assert.Null(ApplicationSettings.ResolveDeviceId("missing",["available"]));
        Assert.Equal("available",ApplicationSettings.ResolveDeviceId("available",["available"]));
    }

    [Fact]
    public void RecordingFolderMustRemainOnALocalFullyQualifiedPath()
    {
        var fallback=new ApplicationSettings().RecordingFolder;
        Assert.Equal(fallback,(new ApplicationSettings{RecordingFolder=@"relative\recordings"}).Validate().RecordingFolder);
        Assert.Equal(fallback,(new ApplicationSettings{RecordingFolder=@"\\server\share\recordings"}).Validate().RecordingFolder);
        Assert.Equal(Path.GetFullPath(@"C:\Recordings"),(new ApplicationSettings{RecordingFolder=@"C:\Recordings"}).Validate().RecordingFolder);
    }

    [Fact]
    public async Task LastSelectedPagePersists() => await WithStore(async (store,_) =>
    {
        await store.SaveAsync(new ApplicationSettings{LastSelectedPage="About"});
        Assert.Equal("About",(await store.LoadAsync()).LastSelectedPage);
    });

    [Fact]
    public async Task RemovedWorkflowSettingsAreDiscardedDuringMigration() => await WithStore(async (store,path) =>
    {
        await File.WriteAllTextAsync(path,"""{"SchemaVersion":4,"LastSelectedPage":"Voice Changer","VirtualOutputDeviceId":"virtual","MonitoringOutputDeviceId":"monitor","MonitoringMode":"Processed voice","MinimizeToTray":true}""");
        var migrated=await store.LoadAsync();
        Assert.Equal(ApplicationSettings.CurrentSchemaVersion,migrated.SchemaVersion);
        Assert.Equal("Recording",migrated.LastSelectedPage);
        var persisted=await File.ReadAllTextAsync(path);
        Assert.Contains($"\"SchemaVersion\": {ApplicationSettings.CurrentSchemaVersion}",persisted);
        Assert.DoesNotContain("VirtualOutput",persisted);
        Assert.DoesNotContain("Monitoring",persisted);
        Assert.DoesNotContain("MinimizeToTray",persisted);
    });

    [Fact]
    public async Task LanguageSettingsRoundTrip() => await WithStore(async (store,_) =>
    {
        await store.SaveAsync(new ApplicationSettings{Language="tr"});
        Assert.Equal("tr",(await store.LoadAsync()).Language);
    });

    [Fact]
    public async Task MissingLegacyLanguageDefaultsToEnglish() => await WithStore(async (store,path) =>
    {
        await File.WriteAllTextAsync(path,"""{"SchemaVersion":4,"LastSelectedPage":"Settings"}""");
        Assert.Equal("en",(await store.LoadAsync()).Language);
    });

    private static async Task WithStore(Func<JsonSettingsStore,string,Task> test)
    {
        var directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"settings.json");
        try{await test(new JsonSettingsStore(path),path);}
        finally{Directory.Delete(directory,true);}
    }
}
