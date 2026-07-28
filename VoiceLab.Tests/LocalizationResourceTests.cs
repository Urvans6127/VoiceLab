using System.Text.RegularExpressions;
using System.Xml.Linq;
using VoiceLab.App.Services;

namespace VoiceLab.Tests;

public sealed class LocalizationResourceTests
{
    private static string Root=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
    private static string AppPath=>Path.Combine(Root,"VoiceLab.App");

    [Fact]
    public void EnglishAndTurkishContainTheSameKeys()
    {
        var english=Keys("en");
        var turkish=Keys("tr");
        Assert.Equal(english,turkish);
        Assert.NotEmpty(english);
    }

    [Fact]
    public void LivePreviewKeysAreCompleteInBothLanguages()
    {
        var required=new[]{"Presets.LivePreview","Presets.StartPreview","Presets.StopPreview","Presets.PreviewDevice","Presets.PreviewHeadphones","Preview.Active","Preview.Stopped","Preview.CouldNotStart","Error.Preview"};
        foreach(var language in new[]{"en","tr"})
        {
            var keys=Keys(language);
            foreach(var key in required)Assert.Contains(key,keys);
        }
    }

    [Fact]
    public void VoiceLabBrandingIsConsistentInEveryLanguage()
    {
        XNamespace x="http://schemas.microsoft.com/winfx/2006/xaml";
        foreach(var language in new[]{"en","tr"})
        {
            var document=XDocument.Load(Path.Combine(AppPath,"Resources",$"Strings.{language}.xaml"));
            string Value(string key)=>document.Root!.Elements().Single(element=>(string?)element.Attribute(x+"Key")==key).Value;
            Assert.Equal("VoiceLab",Value("App.Title"));
            Assert.Equal("Voice",Value("App.BrandLine1"));
            Assert.Equal("Lab",Value("App.BrandLine2"));
            Assert.Equal("VoiceLab",Value("Error.UnhandledTitle"));
        }
    }

    [Fact]
    public void ProductionUiResourcesAndDocumentationContainNoObsoleteRoutingClaims()
    {
        var files=Directory.GetFiles(AppPath,"*.xaml",SearchOption.AllDirectories)
            .Where(path=>!path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",StringComparison.OrdinalIgnoreCase))
            .Concat(new[]{"README.md","PRIVACY.md","SECURITY.md","MANUAL-HARDWARE-VALIDATION.md","THIRD-PARTY-NOTICES.md"}.Select(file=>Path.Combine(Root,file)));
        var obsoleteTerms=new[]{"Local DSP Recorder","Local Voice Changer","Local processing","Yerel işleme","Discord","OBS","VB-CABLE","VoiceMeeter","Virtual output","Virtual microphone","Live routing","Monitoring output"};
        foreach(var path in files)
        {
            var text=File.ReadAllText(path);
            foreach(var term in obsoleteTerms)Assert.DoesNotContain(term,text,StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RuntimeSwitchAndMissingLanguageFallbackWork()
    {
        Exception? failure=null;
        string? english=null;
        string? turkish=null;
        string? fallback=null;
        string? fallbackLanguage=null;
        var thread=new Thread(() =>
        {
            try
            {
                var application=new System.Windows.Application();
                var localization=new LocalizationService();
                var resourceBoundText=new System.Windows.Controls.TextBlock();
                resourceBoundText.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty,"Nav.Settings");
                var window=new System.Windows.Window{Content=resourceBoundText,Width=1,Height=1,Opacity=0,ShowInTaskbar=false,WindowStyle=System.Windows.WindowStyle.None};
                window.Show();
                english=resourceBoundText.Text;
                localization.SetLanguage("tr");
                application.Dispatcher.Invoke(()=>{ },System.Windows.Threading.DispatcherPriority.DataBind);
                turkish=resourceBoundText.Text;
                localization.SetLanguage("zz-ZZ");
                application.Dispatcher.Invoke(()=>{ },System.Windows.Threading.DispatcherPriority.DataBind);
                fallback=resourceBoundText.Text;
                fallbackLanguage=localization.CurrentLanguage;
                window.Close();
                application.Shutdown();
            }
            catch(Exception ex){failure=ex;}
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
        Assert.Equal("Settings",english);
        Assert.Equal("Ayarlar",turkish);
        Assert.Equal("Settings",fallback);
        Assert.Equal(LocalizationService.DefaultLanguage,fallbackLanguage);
    }

    [Fact]
    public void EveryDynamicResourceStringKeyExistsInEnglishFallback()
    {
        var english=Keys("en").ToHashSet(StringComparer.Ordinal);
        var references=Directory.GetFiles(AppPath,"*.xaml",SearchOption.AllDirectories)
            .Where(path=>!path.Contains($"{Path.DirectorySeparatorChar}Resources{Path.DirectorySeparatorChar}Strings.",StringComparison.Ordinal))
            .SelectMany(path=>Regex.Matches(File.ReadAllText(path),@"DynamicResource\s+([A-Z][A-Za-z]+\.[A-Za-z0-9]+)").Select(match=>match.Groups[1].Value))
            .Distinct(StringComparer.Ordinal);
        Assert.DoesNotContain(references,key=>!english.Contains(key));
    }

    [Fact]
    public void ViewsContainNoLiteralUserFacingText()
    {
        var files=Directory.GetFiles(Path.Combine(AppPath,"Views"),"*.xaml")
            .Append(Path.Combine(AppPath,"MainWindow.xaml"));
        var offenders=new List<string>();
        foreach(var path in files)
        {
            var document=XDocument.Load(path);
            foreach(var attribute in document.Descendants().Attributes())
            {
                if(attribute.Name.LocalName is not ("Text" or "Content" or "Header" or "Title" or "ToolTip"))continue;
                if(attribute.Value.StartsWith('{')||!Regex.IsMatch(attribute.Value,"[A-Za-z]{2}"))continue;
                if(Regex.IsMatch(attribute.Value,@"^[\d,.\s]+(?:Hz|ms|dB)$"))continue;
                offenders.Add($"{Path.GetFileName(path)}: {attribute.Name.LocalName}=\"{attribute.Value}\"");
            }
        }
        Assert.DoesNotContain(offenders,_=>true);
    }

    private static string[] Keys(string language)
    {
        XNamespace x="http://schemas.microsoft.com/winfx/2006/xaml";
        return XDocument.Load(Path.Combine(AppPath,"Resources",$"Strings.{language}.xaml"))
            .Root!.Elements()
            .Select(element=>(string?)element.Attribute(x+"Key"))
            .Where(key=>key is not null)
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
