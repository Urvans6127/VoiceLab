using VoiceLab.Infrastructure;

namespace VoiceLab.Tests;

public sealed class PresetTransferTests
{
    private readonly PresetTransferService _service=new();

    [Fact]
    public void ValidPresetImports() { var imported=_service.Import(_service.Export(PresetStore.CreateDefaultCustom() with{Name="My Voice"}),PresetStore.Defaults()); Assert.Equal("My Voice",imported.Name); }

    [Fact]
    public void MalformedPresetIsRejected() => Assert.Throws<PresetValidationException>(()=>_service.Import("{bad",[]));

    [Fact]
    public void UnsupportedSchemaIsRejected()
    {
        var json=_service.Export(PresetStore.CreateDefaultCustom() with{Name="Versioned"}).Replace("\"SchemaVersion\": 1","\"SchemaVersion\": 99");
        Assert.Throws<PresetValidationException>(()=>_service.Import(json,[]));
    }

    [Fact]
    public void ImportedValuesAreClamped()
    {
        var json=_service.Export(PresetStore.CreateDefaultCustom() with{Name="Clamped",PitchSemitones=100,OutputGain=9,EchoFeedback=4});var imported=_service.Import(json,[]);
        Assert.Equal(12,imported.PitchSemitones);Assert.Equal(1,imported.OutputGain);Assert.Equal(.9f,imported.EchoFeedback);
    }

    [Fact]
    public void NonFiniteValuesAreRejected()
    {
        var json=_service.Export(PresetStore.CreateDefaultCustom() with{Name="Finite"}).Replace("\"OutputGain\": 0.85","\"OutputGain\": 1e400");
        Assert.Throws<PresetValidationException>(()=>_service.Import(json,[]));
    }

    [Fact]
    public void ImportedPresetAlwaysBecomesCustom()
    {
        var builtIn=PresetStore.Defaults().First();var imported=_service.Import(_service.Export(builtIn),[]);Assert.False(imported.IsBuiltIn);
    }

    [Fact]
    public void EmptyAndInvalidNamesAreRejected()
    {
        Assert.Throws<PresetValidationException>(()=>PresetTransferService.ValidateName(" ",[]));
        Assert.Throws<PresetValidationException>(()=>PresetTransferService.ValidateName("bad/name",[]));
        Assert.Throws<PresetValidationException>(()=>PresetTransferService.ValidateName(new string('a',121),[]));
    }

    [Fact]
    public void OversizedPresetIsRejectedBeforeDeserialization()
    {
        var json=new string(' ',PresetTransferService.MaxDocumentBytes+1);
        Assert.Throws<PresetValidationException>(()=>_service.Import(json,[]));
    }

    [Fact]
    public void DuplicateNamesAreRejectedCaseInsensitively()
    {
        Assert.Throws<PresetValidationException>(()=>PresetTransferService.ValidateName("clean",PresetStore.Defaults()));
    }

    [Fact]
    public void DuplicateBuiltInCreatesIndependentCustomCopy()
    {
        var builtIn=PresetStore.Defaults().First(p=>p.Name=="Natural Male");var copy=PresetStore.Sanitize(builtIn with{Name="My Male",IsBuiltIn=false});
        Assert.False(copy.IsBuiltIn);Assert.Equal(builtIn.PitchSemitones,copy.PitchSemitones);Assert.Equal("Natural Male",builtIn.Name);
    }

    [Fact]
    public void RenameValidationAllowsCurrentPresetButRejectsAnother()
    {
        var one=PresetStore.CreateDefaultCustom() with{Name="One"};var two=PresetStore.CreateDefaultCustom() with{Name="Two"};
        PresetTransferService.ValidateName("One",[one,two],one);
        Assert.Throws<PresetValidationException>(()=>PresetTransferService.ValidateName("two",[one,two],one));
    }

    [Fact]
    public void EditingBuiltInProducesCustomModifiedTargetWithoutMutation()
    {
        var presets=PresetStore.Defaults();var builtIn=presets.First(p=>p.IsBuiltIn);var target=PresetStore.SelectForEdit(builtIn,presets);
        Assert.False(target.IsBuiltIn);Assert.Equal("Custom",target.Name);Assert.True(builtIn.IsBuiltIn);
    }
}
