using System.Text.Json;

namespace VoiceLab.Infrastructure;

public sealed class PresetStore
{
    public const string CustomName = "Custom";
    private const long MaxStoreBytes = 4 * 1024 * 1024;
    private readonly string _path;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public PresetStore(string? path = null) =>
        _path = path ?? VoiceLabStoragePaths.PresetsFile;

    public IReadOnlyList<VoicePreset> Load()
    {
        try
        {
            if (!File.Exists(_path)) return Defaults();
            if (new FileInfo(_path).Length > MaxStoreBytes)
            {
                Quarantine();
                return Defaults();
            }
            var stored = JsonSerializer.Deserialize<List<VoicePreset>>(File.ReadAllText(_path), Options) ?? [];
            var custom = stored.FirstOrDefault(IsCustom) is { } savedCustom ? Sanitize(savedCustom with { Name = CustomName, IsBuiltIn = false }) : CreateDefaultCustom();
            var namedCustoms = stored.Where(p => !p.IsBuiltIn && !IsCustom(p)).Select(Sanitize);
            return BuiltIns().Append(custom).Concat(namedCustoms).ToArray();
        }
        catch (JsonException)
        {
            Quarantine();
            return Defaults();
        }
        catch (IOException)
        {
            return Defaults();
        }
        catch (UnauthorizedAccessException)
        {
            return Defaults();
        }
    }

    public void Save(IEnumerable<VoicePreset> presets)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var writable = presets.Where(p => !p.IsBuiltIn).Select(Sanitize).ToArray();
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(writable, Options));
            File.Move(temp, _path, true);
        }
        finally
        {
            try { File.Delete(temp); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static IReadOnlyList<VoicePreset> Defaults() => BuiltIns().Append(CreateDefaultCustom()).ToArray();

    public static VoicePreset CreateDefaultCustom() =>
        Make(CustomName, isBuiltIn: false, outputGain: .85f);

    public static bool IsCustom(VoicePreset preset) =>
        !preset.IsBuiltIn && string.Equals(preset.Name, CustomName, StringComparison.OrdinalIgnoreCase);

    public static VoicePreset SelectForEdit(VoicePreset current, IEnumerable<VoicePreset> presets) =>
        IsCustom(current) ? current : presets.First(IsCustom);

    private void Quarantine()
    {
        try { File.Move(_path, _path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static IReadOnlyList<VoicePreset> BuiltIns() =>
    [
        Make("Clean", outputGain: .9f),
        Make("Natural Male", pitch: -1.5f, depth: .25f, brightness: -.05f, bass: 1.5f, treble: -.5f, toneMix: .65f, saturation: .02f, outputGain: .82f),
        Make("Deep Natural Male", pitch: -2.5f, depth: .4f, brightness: -.1f, bass: 2, treble: -1, toneMix: .7f, saturation: .03f, outputGain: .78f),
        Make("Young Male", pitch: .7f, depth: -.1f, brightness: .12f, bass: -.5f, treble: .8f, toneMix: .55f, saturation: .01f, outputGain: .82f),
        Make("Natural Female", pitch: 1.7f, depth: -.25f, brightness: .15f, bass: -1, treble: 1, toneMix: .6f, saturation: .01f, outputGain: .8f),
        Make("Soft Female", pitch: 1.2f, depth: -.12f, brightness: -.08f, bass: -.5f, treble: -.5f, toneMix: .5f, reverbMix: .03f, outputGain: .8f),
        Make("Bright Female", pitch: 2, depth: -.28f, brightness: .25f, bass: -1.5f, treble: 1.8f, toneMix: .65f, outputGain: .77f),
        Make("Deep Voice", pitch: -3, depth: .65f, brightness: -.15f, bass: 4, treble: -2, toneMix: .8f, outputGain: .72f, reverbMix: .06f),
        Make("Very Deep Voice", pitch: -6, depth: .9f, brightness: -.3f, bass: 6, treble: -4, toneMix: .9f, saturation: .12f, outputGain: .62f, reverbMix: .08f),
        Make("High Voice", pitch: 4, depth: -.35f, brightness: .25f, bass: -2, treble: 2, toneMix: .7f, outputGain: .75f),
        Make("Very High Voice", pitch: 7, depth: -.65f, brightness: .45f, bass: -4, treble: 4, toneMix: .8f, outputGain: .65f),
        Make("Soft Voice", depth: .15f, brightness: -.35f, bass: 1, treble: -4, toneMix: .7f, outputGain: .78f, reverbMix: .12f),
        Make("Bright Voice", pitch: 1, depth: -.1f, brightness: .7f, bass: -1, treble: 5, toneMix: .8f, outputGain: .72f),
        Make("Dark Voice", depth: .4f, brightness: -.65f, bass: 3, treble: -6, toneMix: .85f, outputGain: .7f),
        Make("Radio Voice", depth: -.2f, brightness: .1f, bass: -12, treble: -6, toneMix: 1, saturation: .45f, outputGain: .6f),
        Make("Robot Voice", brightness: .2f, bass: -2, treble: 2, toneMix: .65f, saturation: .25f, robotMix: .75f, outputGain: .6f),
        Make("Echo Voice", brightness: .1f, toneMix: .35f, echoMix: .35f, echoFeedback: .28f, outputGain: .7f)
    ];

    private static VoicePreset Make(string name, bool isBuiltIn = true, float pitch = 0, float depth = 0, float brightness = 0, float bass = 0, float treble = 0, float toneMix = 0, float saturation = 0, float outputGain = .8f, float robotMix = 0, float echoMix = 0, float echoFeedback = .3f, float reverbMix = 0) =>
        new(name, isBuiltIn, 1, outputGain, true, -45, 5, 100, pitch != 0, pitch, robotMix > 0, 70, robotMix, echoMix > 0, 250, echoFeedback, echoMix, reverbMix > 0, .5f, reverbMix, depth, brightness, bass, treble, toneMix, saturation);

    public static VoicePreset Sanitize(VoicePreset preset) => preset with
    {
        IsBuiltIn = false,
        InputGain = Math.Clamp(preset.InputGain, 0, 2),
        OutputGain = Math.Clamp(preset.OutputGain, 0, 1),
        GateThresholdDb = Math.Clamp(preset.GateThresholdDb, -80, 0),
        GateAttackMs = Math.Clamp(preset.GateAttackMs, .1f, 100),
        GateReleaseMs = Math.Clamp(preset.GateReleaseMs, 1, 1000),
        PitchSemitones = Math.Clamp(preset.PitchSemitones, -12, 12),
        RobotFrequency = Math.Clamp(preset.RobotFrequency, 20, 1000),
        RobotMix = Math.Clamp(preset.RobotMix, 0, 1),
        EchoDelayMs = Math.Clamp(preset.EchoDelayMs, 10, 1500),
        EchoFeedback = Math.Clamp(preset.EchoFeedback, 0, .9f),
        EchoMix = Math.Clamp(preset.EchoMix, 0, 1),
        ReverbRoomSize = Math.Clamp(preset.ReverbRoomSize, 0, 1),
        ReverbMix = Math.Clamp(preset.ReverbMix, 0, .8f),
        VoiceDepth = Math.Clamp(preset.VoiceDepth, -1, 1),
        Brightness = Math.Clamp(preset.Brightness, -1, 1),
        BassDb = Math.Clamp(preset.BassDb, -12, 12),
        TrebleDb = Math.Clamp(preset.TrebleDb, -12, 12),
        ToneMix = Math.Clamp(preset.ToneMix, 0, 1),
        Saturation = Math.Clamp(preset.Saturation, 0, 1)
    };
}
