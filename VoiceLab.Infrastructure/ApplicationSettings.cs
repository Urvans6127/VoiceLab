namespace VoiceLab.Infrastructure;

public sealed record ApplicationSettings
{
    public const int CurrentSchemaVersion = 6;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string? LastInputDeviceId { get; init; }
    public string? LastPreviewOutputDeviceId { get; init; }
    public string LastSelectedPreset { get; init; } = "Clean";
    public double WindowWidth { get; init; } = 1120;
    public double WindowHeight { get; init; } = 860;
    public double? WindowLeft { get; init; }
    public double? WindowTop { get; init; }
    public bool WindowMaximized { get; init; }
    public string RecordingFolder { get; init; } = VoiceLabStoragePaths.RecordingsDirectory;
    public string LatencyProfile { get; init; } = "Balanced";
    public int PreferredSampleRate { get; init; } = 48000;
    public int RequestedBufferMilliseconds { get; init; } = 50;
    public string LastSelectedPage { get; init; } = "Recording";
    public string Language { get; init; } = "en";

    public ApplicationSettings Validate()
    {
        static bool Valid(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;
        static double? Position(double? value) => value is { } v && double.IsFinite(v) && v is >= -10000 and <= 10000 ? v : null;
        string[] pages = ["Recording", "Presets", "Devices", "Settings", "About"];
        string[] profiles = ["Safe", "Balanced", "Low Latency"];
        return this with
        {
            SchemaVersion = CurrentSchemaVersion,
            WindowWidth = Valid(WindowWidth, 800, 5000) ? WindowWidth : 1120,
            WindowHeight = Valid(WindowHeight, 600, 5000) ? WindowHeight : 860,
            WindowLeft = Position(WindowLeft),
            WindowTop = Position(WindowTop),
            LastSelectedPreset = string.IsNullOrWhiteSpace(LastSelectedPreset) ? "Clean" : LastSelectedPreset.Trim(),
            RecordingFolder = NormalizeLocalRecordingFolder(RecordingFolder),
            LatencyProfile = profiles.Contains(LatencyProfile) ? LatencyProfile : "Balanced",
            PreferredSampleRate = PreferredSampleRate is 44100 or 48000 ? PreferredSampleRate : 48000,
            RequestedBufferMilliseconds = LatencyProfile switch { "Safe" => 100, "Low Latency" => 25, _ => 50 },
            LastSelectedPage = pages.Contains(LastSelectedPage) ? LastSelectedPage : "Recording",
            Language = string.IsNullOrWhiteSpace(Language) ? "en" : Language.Trim().ToLowerInvariant()
        };
    }

    public static string? ResolveDeviceId(string? savedId, IEnumerable<string> availableIds) =>
        !string.IsNullOrWhiteSpace(savedId) && availableIds.Contains(savedId, StringComparer.Ordinal) ? savedId : null;

    public static string NormalizeLocalRecordingFolder(string? path)
    {
        var fallback=VoiceLabStoragePaths.RecordingsDirectory;
        if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path))return fallback;
        try
        {
            var full=Path.GetFullPath(path.Trim());
            return full.StartsWith(@"\\",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(Path.GetPathRoot(full))?fallback:full;
        }
        catch(Exception ex) when(ex is ArgumentException or NotSupportedException or PathTooLongException){return fallback;}
    }
}
