using System.Xml.Linq;

namespace VoiceLab.Tests;

public sealed class ComboBoxPresentationTests
{
    private static string Root=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
    private static string Read(params string[] parts)=>File.ReadAllText(Path.Combine([Root,..parts]));

    [Fact] public void RecordingPresetSelectorDisplaysPresetName()
    {
        var view=Read("VoiceLab.App","Views","RecordingView.xaml");
        Assert.Contains("DisplayMemberPath=\"Name\"",view);
        Assert.DoesNotContain("DisplayMemberPath=\"VoicePreset\"",view);
    }

    [Fact] public void AudioDeviceSelectorsUseFriendlyDisplayName()
    {
        var view=Read("VoiceLab.App","Views","DevicesView.xaml");
        Assert.Contains("x:Key=\"AudioDeviceComboTemplate\"",view);
        Assert.Contains("Text=\"{Binding DisplayName, Mode=OneWay}\"",view);
        Assert.Contains("TextWrapping=\"NoWrap\" TextTrimming=\"None\"",view);
        Assert.DoesNotContain("MaxWidth=\"430\"",view);
    }

    [Fact] public void MicrophonePopupExpandsWithoutChangingSharedComboBoxes()
    {
        var view=Read("VoiceLab.App","Views","DevicesView.xaml");
        Assert.Contains("x:Key=\"MicrophoneComboBoxStyle\"",view);
        Assert.Contains("Style=\"{StaticResource MicrophoneComboBoxStyle}\"",view);
        Assert.Equal(1,view.Split("Style=\"{StaticResource MicrophoneComboBoxStyle}\"",StringSplitOptions.None).Length-1);
        Assert.Contains("MinWidth=\"{Binding ActualWidth, ElementName=DropDownToggle, Mode=OneWay}\"",view);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"",view);
    }

    [Fact] public void StartupStatusRunBindingsAreExplicitlyOneWay()
    {
        var view=Read("VoiceLab.App","Views","RecordingView.xaml");
        Assert.Contains("Text=\"{Binding Workspace.Format, Mode=OneWay}\"",view);
        Assert.Contains("Text=\"{Binding Workspace.Latency, Mode=OneWay}\"",view);
        Assert.Contains("Text=\"{Binding Workspace.SelectedLatencyProfileDisplay, Mode=OneWay}\"",view);
    }

    [Fact] public void AdvancedDspControlsUseOneSessionPersistentHeaderToggle()
    {
        var view=Read("VoiceLab.App","Views","RecordingView.xaml");
        var codeBehind=Read("VoiceLab.App","Views","RecordingView.xaml.cs");
        var viewModel=Read("VoiceLab.App","ViewModels","RecordingViewModel.cs");
        Assert.Equal(1,view.Split("Text=\"{DynamicResource Dsp.Controls}\"",StringSplitOptions.None).Length-1);
        Assert.DoesNotContain("DynamicResource Dsp.Effects",view);
        Assert.Contains("IsChecked=\"{Binding IsAdvancedControlsExpanded, Mode=TwoWay}\"",view);
        Assert.Contains("Text=\"{Binding AdvancedControlsSummary, Mode=OneWay}\"",view);
        Assert.Contains("x:Name=\"AdvancedContent\" Visibility=\"Collapsed\"",view);
        Assert.True(view.IndexOf("</ToggleButton>",StringComparison.Ordinal)<view.IndexOf("x:Name=\"AdvancedContent\"",StringComparison.Ordinal));
        Assert.Contains("TimeSpan.FromMilliseconds(180)",codeBehind);
        Assert.Contains("private bool _isAdvancedControlsExpanded",viewModel);
        Assert.Contains("Workspace.GateEnabled",viewModel);
    }

    [Fact] public void DevicesPageContainsOnlyMicrophoneAndQualityConfiguration()
    {
        var view=Read("VoiceLab.App","Views","DevicesView.xaml");
        Assert.Contains("Workspace.Inputs",view);
        Assert.Contains("Workspace.SelectedLatencyProfile",view);
        Assert.DoesNotContain("Monitoring",view,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VirtualOutput",view,StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void PresetsPagePlacesCompactLocalPreviewBeforeManagementActions()
    {
        var view=Read("VoiceLab.App","Views","PresetsView.xaml");
        var selected=view.IndexOf("Presets.Selected",StringComparison.Ordinal);
        var preview=view.IndexOf("Presets.LivePreview",StringComparison.Ordinal);
        var actions=view.IndexOf("Presets.NameActions",StringComparison.Ordinal);
        var importExport=view.IndexOf("Presets.ImportExport",StringComparison.Ordinal);
        var custom=view.IndexOf("Presets.CustomDefaults",StringComparison.Ordinal);
        Assert.True(selected>=0&&selected<preview&&preview<actions&&actions<importExport&&importExport<custom);
        Assert.Contains("Workspace.Presets",view);
        Assert.Contains("Common.BuiltIn",view);
        Assert.Contains("Common.Custom",view);
        Assert.Contains("StartPreviewCommand",view);
        Assert.Contains("StopPreviewCommand",view);
        Assert.Contains("Workspace.SelectedPreviewOutput",view);
        Assert.Contains("Workspace.InputLevel",view);
        Assert.Contains("Workspace.OutputLevel",view);
        Assert.DoesNotContain("VirtualOutput",view,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LiveRouting",view,StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void UserFacingValueSelectorsUseLabels()
    {
        var view=Read("VoiceLab.App","Views","DevicesView.xaml");
        Assert.Contains("Low Latency",Read("VoiceLab.App","MainViewModel.cs"));
        Assert.Contains("Content=\"48,000 Hz\" Tag=\"48000\"",view);
        Assert.Contains("Content=\"44,100 Hz\" Tag=\"44100\"",view);
        Assert.Contains("SelectedValuePath=\"Tag\"",view);
    }

    [Fact] public void ComboBoxSurfaceAndPopupAreExplicitDarkBrushes()
    {
        var theme=Read("VoiceLab.App","Resources","Theme.xaml");
        var document=XDocument.Parse(theme);
        XNamespace x="http://schemas.microsoft.com/winfx/2006/xaml";
        string Color(string key)=>document.Descendants().Single(e=>(string?)e.Attribute(x+"Key")==key).Attribute("Color")!.Value;
        Assert.Equal("#101A26",Color("ComboBoxBackgroundBrush"));
        Assert.Equal("#182433",Color("ComboBoxPopupBackgroundBrush"));
        Assert.DoesNotContain(Color("ComboBoxBackgroundBrush"),new[]{"#FFFFFF","#FFF","White"});
        Assert.Contains("Background=\"{StaticResource ComboBoxPopupBackgroundBrush}\"",theme);
    }

    [Fact] public void RequiredComboBoxBrushesExistAndEveryCriticalTemplateValueIsConcrete()
    {
        var theme=Read("VoiceLab.App","Resources","Theme.xaml");
        foreach(var resource in new[]{"ComboBoxBackgroundBrush","ComboBoxForegroundBrush","ComboBoxBorderBrush","ComboBoxHoverBackgroundBrush","ComboBoxFocusedBorderBrush","ComboBoxDisabledBackgroundBrush","ComboBoxDisabledForegroundBrush","ComboBoxPopupBackgroundBrush","ComboBoxItemBackgroundBrush","ComboBoxItemForegroundBrush","ComboBoxItemHoverBackgroundBrush","ComboBoxItemHoverForegroundBrush","ComboBoxItemSelectedBackgroundBrush","ComboBoxItemSelectedForegroundBrush","ComboBoxArrowBrush"})Assert.Contains($"x:Key=\"{resource}\"",theme);
        Assert.DoesNotContain("Background=\"{TemplateBinding Background}\"",theme);
        Assert.DoesNotContain("TextElement.Foreground=\"{TemplateBinding Foreground}\"",theme);
        Assert.DoesNotContain("Fill=\"{TemplateBinding Foreground}\"",theme);
    }

    [Fact] public void ComboBoxPopupMatchesOwningControlWidth()
    {
        var theme=Read("VoiceLab.App","Resources","Theme.xaml");
        Assert.Contains("MaxHeight\" Value=\"40\"",theme);
        Assert.Contains("MaxHeight=\"320\"",theme);
        Assert.Contains("PlacementTarget=\"{Binding ElementName=DropDownToggle}\"",theme);
        Assert.Contains("Width=\"{Binding ActualWidth, ElementName=DropDownToggle, Mode=OneWay}\"",theme);
        Assert.DoesNotContain("Width=\"420\"",theme);
    }
}
