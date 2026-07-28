using VoiceLab.App.Services;

namespace VoiceLab.App.ViewModels;

public sealed class ShellViewModel : ViewModelBase
{
    private readonly SettingsCoordinator _settings;
    private NavigationItemViewModel _selectedItem;
    public IReadOnlyList<NavigationItemViewModel> Items { get; }
    public NavigationItemViewModel SelectedItem
    {
        get => _selectedItem;
        set { if (Set(ref _selectedItem, value)) { Notify(nameof(CurrentPage)); _settings.Update(s => s with { LastSelectedPage = value.Id }); } }
    }
    public object CurrentPage => SelectedItem.Page;

    public ShellViewModel(PresetsViewModel presets, RecordingViewModel recording, DevicesViewModel devices, SettingsViewModel settings, AboutViewModel about, SettingsCoordinator coordinator, LocalizationService localization)
    {
        _settings = coordinator;
        Items =
        [
            new("Recording", "Nav.Recording", "●", recording, localization),
            new("Presets", "Nav.Presets", "★", presets, localization),
            new("Devices", "Nav.Devices", "⌁", devices, localization),
            new("Settings", "Nav.Settings", "⚙", settings, localization),
            new("About", "Nav.About", "ⓘ", about, localization)
        ];
        _selectedItem = Items.FirstOrDefault(item => item.Id == coordinator.Current.LastSelectedPage) ?? Items[0];
    }
}
