using System.Text.Json;

namespace VoiceLab.Infrastructure;

public sealed class PresetTransferService
{
    public const int MaxDocumentBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public VoicePreset ImportFile(string path, IEnumerable<VoicePreset> existing)
    {
        var length = new FileInfo(path).Length;
        if (length > MaxDocumentBytes) throw new PresetValidationException("The preset file is too large.");
        return Import(File.ReadAllText(path), existing);
    }

    public VoicePreset Import(string json, IEnumerable<VoicePreset> existing)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxDocumentBytes) throw new PresetValidationException("The preset file is too large.");
        PresetDocument document;
        try { document = JsonSerializer.Deserialize<PresetDocument>(json, Options) ?? throw new PresetValidationException("The preset file is empty."); }
        catch (JsonException ex) { throw new PresetValidationException($"The preset JSON is invalid: {ex.Message}"); }
        if (document.SchemaVersion != PresetDocument.CurrentSchemaVersion) throw new PresetValidationException("The preset schema version is not supported.");
        ValidateName(document.Name, existing);
        ValidateFinite(document.Parameters);
        return PresetStore.Sanitize(document.Parameters with { Name = document.Name.Trim(), IsBuiltIn = false });
    }

    public string Export(VoicePreset preset)
    {
        var document = new PresetDocument { Name = preset.Name, IsBuiltIn = false, Parameters = preset with { IsBuiltIn = false } };
        return JsonSerializer.Serialize(document, Options);
    }

    public async Task ExportAsync(VoicePreset preset, string path, CancellationToken cancellationToken = default)
    {
        if (File.Exists(path)) throw new IOException("The export file already exists.");
        await File.WriteAllTextAsync(path, Export(preset), cancellationToken);
    }

    public static void ValidateName(string? name, IEnumerable<VoicePreset> existing, VoicePreset? except = null)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) throw new PresetValidationException("Preset names cannot be empty.");
        if (trimmed.Length > 120) throw new PresetValidationException("Preset names cannot exceed 120 characters.");
        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new PresetValidationException("The preset name contains invalid filename characters.");
        if (existing.Any(p => !ReferenceEquals(p, except) && string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase))) throw new PresetValidationException("A preset with this name already exists.");
    }

    private static void ValidateFinite(VoicePreset p)
    {
        float[] values = [p.InputGain,p.OutputGain,p.GateThresholdDb,p.GateAttackMs,p.GateReleaseMs,p.PitchSemitones,p.RobotFrequency,p.RobotMix,p.EchoDelayMs,p.EchoFeedback,p.EchoMix,p.ReverbRoomSize,p.ReverbMix,p.VoiceDepth,p.Brightness,p.BassDb,p.TrebleDb,p.ToneMix,p.Saturation];
        if (values.Any(v => !float.IsFinite(v))) throw new PresetValidationException("Preset values must be finite numbers.");
    }
}
