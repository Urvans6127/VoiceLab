using System.Xml.Linq;

namespace VoiceLab.Tests;

public sealed class ScrollBarTemplateTests
{
    private static readonly XNamespace Presentation="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X="http://schemas.microsoft.com/winfx/2006/xaml";
    private static string Root=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
    private static string ThemePath=>Path.Combine(Root,"VoiceLab.App","Resources","Theme.xaml");
    private static XDocument Theme()=>XDocument.Load(ThemePath,LoadOptions.PreserveWhitespace);

    [Fact]
    public void SharedScrollBarTemplatesContainRequiredNativeParts()
    {
        var document=Theme();
        var tracks=document.Descendants(Presentation+"Track").Where(e=>(string?)e.Attribute(X+"Name")=="PART_Track").ToArray();

        Assert.Equal(2,tracks.Length);
        Assert.All(tracks,track=>
        {
            Assert.Equal("{TemplateBinding Orientation}",(string?)track.Attribute("Orientation"));
            Assert.Equal("{TemplateBinding Minimum}",(string?)track.Attribute("Minimum"));
            Assert.Equal("{TemplateBinding Maximum}",(string?)track.Attribute("Maximum"));
            Assert.Equal("{TemplateBinding Value}",(string?)track.Attribute("Value"));
            Assert.Equal("{TemplateBinding ViewportSize}",(string?)track.Attribute("ViewportSize"));
            Assert.Single(track.Descendants(Presentation+"Thumb"));
            Assert.Single(track.Elements(Presentation+"Track.DecreaseRepeatButton"));
            Assert.Single(track.Elements(Presentation+"Track.IncreaseRepeatButton"));
        });
    }

    [Fact]
    public void SharedScrollBarTemplatesContainBothLineButtonsAndPagingCommands()
    {
        var text=File.ReadAllText(ThemePath);
        foreach(var command in new[]{"LineUpCommand","LineDownCommand","PageUpCommand","PageDownCommand","LineLeftCommand","LineRightCommand","PageLeftCommand","PageRightCommand"})
            Assert.Contains($"ScrollBar.{command}",text);

        Assert.True(text.Split("AncestorType={x:Type ScrollBar}",StringSplitOptions.None).Length-1>=8);
    }

    [Fact]
    public void ScrollBarCriticalBackgroundsAreConcreteAndResourcesExist()
    {
        var document=Theme();
        foreach(var key in new[]{"ScrollBarTrackBrush","ScrollBarThumbBrush","ScrollBarThumbHoverBrush","ScrollBarThumbPressedBrush"})
        {
            var brush=document.Descendants(Presentation+"SolidColorBrush").Single(e=>(string?)e.Attribute(X+"Key")==key);
            Assert.StartsWith("#",(string?)brush.Attribute("Color"));
        }

        foreach(var border in document.Descendants(Presentation+"Border").Where(e=>(string?)e.Attribute(X+"Name") is "Chrome" or "ThumbChrome"))
        {
            var background=(string?)border.Attribute("Background");
            Assert.False(string.IsNullOrWhiteSpace(background));
            Assert.DoesNotContain("TemplateBinding",background);
            Assert.DoesNotContain("Binding",background);
        }
    }
}
