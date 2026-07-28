using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace VoiceLab.App.Views;

public partial class RecordingView:System.Windows.Controls.UserControl
{
    private static readonly Duration AdvancedAnimationDuration=new(TimeSpan.FromMilliseconds(180));
    private int _advancedAnimationVersion;

    public RecordingView()=>InitializeComponent();

    private void AdvancedToggle_Checked(object sender,RoutedEventArgs e)
    {
        var version=++_advancedAnimationVersion;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>ExpandAdvancedControls(version)));
    }

    private void AdvancedToggle_Unchecked(object sender,RoutedEventArgs e)
    {
        var version=++_advancedAnimationVersion;
        if(AdvancedContent is null||AdvancedContent.Visibility!=Visibility.Visible)return;
        AdvancedContent.BeginAnimation(HeightProperty,null);
        AdvancedContent.BeginAnimation(OpacityProperty,null);
        var currentHeight=Math.Max(0,AdvancedContent.ActualHeight);
        if(currentHeight<=0){HideAdvancedControls(version);return;}
        AdvancedContent.Height=currentHeight;
        var easing=new CubicEase{EasingMode=EasingMode.EaseInOut};
        var heightAnimation=new DoubleAnimation(currentHeight,0,AdvancedAnimationDuration){EasingFunction=easing};
        var opacityAnimation=new DoubleAnimation(AdvancedContent.Opacity,0,AdvancedAnimationDuration){EasingFunction=easing};
        heightAnimation.Completed+=(_,_)=>HideAdvancedControls(version);
        AdvancedContent.BeginAnimation(HeightProperty,heightAnimation);
        AdvancedContent.BeginAnimation(OpacityProperty,opacityAnimation);
    }

    private void ExpandAdvancedControls(int version)
    {
        if(version!=_advancedAnimationVersion||AdvancedToggle.IsChecked!=true)return;
        AdvancedContent.BeginAnimation(HeightProperty,null);
        AdvancedContent.BeginAnimation(OpacityProperty,null);
        AdvancedContent.Visibility=Visibility.Visible;
        AdvancedContent.Height=double.NaN;
        AdvancedContent.Measure(new System.Windows.Size(Math.Max(1,AdvancedToggle.ActualWidth),double.PositiveInfinity));
        var targetHeight=Math.Max(1,AdvancedContent.DesiredSize.Height);
        AdvancedContent.Height=0;
        AdvancedContent.Opacity=0;
        var easing=new CubicEase{EasingMode=EasingMode.EaseOut};
        var heightAnimation=new DoubleAnimation(0,targetHeight,AdvancedAnimationDuration){EasingFunction=easing};
        var opacityAnimation=new DoubleAnimation(0,1,AdvancedAnimationDuration){EasingFunction=easing};
        heightAnimation.Completed+=(_,_)=>
        {
            if(version!=_advancedAnimationVersion||AdvancedToggle.IsChecked!=true)return;
            AdvancedContent.BeginAnimation(HeightProperty,null);
            AdvancedContent.Height=double.NaN;
            AdvancedContent.Opacity=1;
        };
        AdvancedContent.BeginAnimation(HeightProperty,heightAnimation);
        AdvancedContent.BeginAnimation(OpacityProperty,opacityAnimation);
    }

    private void HideAdvancedControls(int version)
    {
        if(version!=_advancedAnimationVersion||AdvancedToggle.IsChecked==true)return;
        AdvancedContent.BeginAnimation(HeightProperty,null);
        AdvancedContent.BeginAnimation(OpacityProperty,null);
        AdvancedContent.Height=double.NaN;
        AdvancedContent.Opacity=0;
        AdvancedContent.Visibility=Visibility.Collapsed;
    }
}
