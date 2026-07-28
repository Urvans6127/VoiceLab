namespace VoiceLab.Infrastructure;

public sealed record PresetDocument
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "Imported Preset";
    public bool IsBuiltIn { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
    public VoicePreset Parameters { get; init; } = PresetStore.CreateDefaultCustom();
}

public sealed class PresetValidationException(string message) : Exception(message);
