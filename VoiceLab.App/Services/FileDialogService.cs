using Microsoft.Win32;
using System.IO;

namespace VoiceLab.App.Services;

public sealed class FileDialogService
{
    private readonly LocalizationService _localization;
    public FileDialogService(LocalizationService localization) => _localization = localization;

    public string? ChoosePresetToOpen()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = _localization.Get("Dialog.ImportPreset"), Filter = _localization.Get("Dialog.PresetFilter"), CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ChoosePresetToExport(string name)
    {
        var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = _localization.Get("Dialog.ExportPreset"), Filter = _localization.Get("Dialog.PresetFilter"), FileName = safe + ".json", OverwritePrompt = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ChooseFolder(string initial)
    {
        var dialog = new OpenFolderDialog { Title = _localization.Get("Dialog.ChooseRecordingFolder"), InitialDirectory = initial, Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
