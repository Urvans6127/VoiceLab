using VoiceLab.Infrastructure;

namespace VoiceLab.Tests;

public sealed class PresetStoreTests
{
    [Fact]
    public void CustomPresetPersistsAfterReload()
    {
        WithStore((store, path) =>
        {
            var custom = PresetStore.CreateDefaultCustom() with { PitchSemitones = 2.25f, VoiceDepth = -.2f, OutputGain = .74f, EchoEnabled = true, EchoMix = .08f };
            store.Save([custom]);
            var reloaded = new PresetStore(path).Load().Single(PresetStore.IsCustom);
            Assert.Equal(2.25f, reloaded.PitchSemitones);
            Assert.Equal(-.2f, reloaded.VoiceDepth);
            Assert.Equal(.74f, reloaded.OutputGain);
            Assert.True(reloaded.EchoEnabled);
            Assert.Equal(.08f, reloaded.EchoMix);
        });
    }

    [Fact]
    public void MissingCustomIsRecreatedSafely()
    {
        WithStore((store, _) =>
        {
            store.Save([]);
            var custom = store.Load().Single(PresetStore.IsCustom);
            Assert.Equal(PresetStore.CustomName, custom.Name);
            Assert.False(custom.IsBuiltIn);
            Assert.Equal(.85f, custom.OutputGain);
        });
    }

    [Fact]
    public void CorruptedPresetFileReturnsSafeCustomAndIsQuarantined()
    {
        WithStore((store, path) =>
        {
            File.WriteAllText(path, "not json");
            var result = store.Load();
            Assert.Contains(result, PresetStore.IsCustom);
            Assert.NotEmpty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.corrupt-*"));
        });
    }

    [Fact]
    public void OversizedPresetStoreReturnsDefaultsAndIsQuarantined()
    {
        WithStore((store, path) =>
        {
            using (var stream = new FileStream(path, FileMode.CreateNew))
                stream.SetLength(4L * 1024 * 1024 + 1);
            var result = store.Load();
            Assert.Contains(result, PresetStore.IsCustom);
            Assert.NotEmpty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.corrupt-*"));
        });
    }

    [Fact]
    public void StoredPresetCannotOverwriteBuiltIn()
    {
        WithStore((store, _) =>
        {
            var forgedBuiltIn = PresetStore.Defaults().First(p => p.Name == "Clean") with { OutputGain = 0, PitchSemitones = 12 };
            store.Save([forgedBuiltIn, PresetStore.CreateDefaultCustom()]);
            var clean = store.Load().First(p => p.Name == "Clean");
            Assert.Equal(.9f, clean.OutputGain);
            Assert.Equal(0, clean.PitchSemitones);
        });
    }

    [Fact]
    public void EditingBuiltInSelectsCustomWithoutMutatingEitherPreset()
    {
        var presets = PresetStore.Defaults();
        var builtIn = presets.First(p => p.Name == "Natural Male");
        var custom = presets.Single(PresetStore.IsCustom);
        var selected = PresetStore.SelectForEdit(builtIn, presets);
        Assert.Same(custom, selected);
        Assert.Equal("Natural Male", builtIn.Name);
        Assert.Equal(PresetStore.CustomName, selected.Name);
    }

    [Fact]
    public void UnsafeCustomValuesAreClampedOnReload()
    {
        WithStore((store, _) =>
        {
            var unsafeCustom = PresetStore.CreateDefaultCustom() with { OutputGain = 4, PitchSemitones = 50, EchoFeedback = 2, BassDb = 30, Saturation = 3 };
            store.Save([unsafeCustom]);
            var loaded = store.Load().Single(PresetStore.IsCustom);
            Assert.Equal(1, loaded.OutputGain);
            Assert.Equal(12, loaded.PitchSemitones);
            Assert.Equal(.9f, loaded.EchoFeedback);
            Assert.Equal(12, loaded.BassDb);
            Assert.Equal(1, loaded.Saturation);
        });
    }

    [Fact]
    public void DefaultsContainNaturalAndEffectPresetsWithSafeLevels()
    {
        var defaults = PresetStore.Defaults();
        string[] expected = ["Natural Male", "Deep Natural Male", "Young Male", "Natural Female", "Soft Female", "Bright Female", "Deep Voice", "Very Deep Voice", "High Voice", "Very High Voice", "Soft Voice", "Bright Voice", "Dark Voice", "Radio Voice", "Robot Voice", "Echo Voice", "Custom"];
        Assert.All(expected, name => Assert.Contains(defaults, preset => preset.Name == name));
        Assert.All(defaults, preset => Assert.InRange(preset.OutputGain, 0, .9f));
        Assert.All(defaults.Where(p => p.Name.Contains("Male") || p.Name.Contains("Female")), preset =>
        {
            Assert.InRange(preset.PitchSemitones, -2.5f, 2f);
            Assert.False(preset.RobotEnabled);
            Assert.False(preset.EchoEnabled);
            Assert.InRange(preset.ReverbMix, 0, .03f);
            Assert.InRange(preset.Saturation, 0, .03f);
        });
    }

    private static void WithStore(Action<PresetStore, string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "presets.json");
            var store = new PresetStore(path);
            test(store, path);
        }
        finally { Directory.Delete(directory, true); }
    }
}
