using VoiceLab.App.Services;
using VoiceLab.App.ViewModels;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VoiceLab.App;

public partial class MainWindow : Window
{
    private const int WmGetMinMaxInfo=0x0024;
    private const uint MonitorDefaultToNearest=0x00000002;
    private readonly SettingsCoordinator _settings;private readonly LocalizationService _localization;private readonly bool _startMaximized;private bool _allowClose;private HwndSource? _windowSource;
    public event Func<Task>? ExitRequested;
    public MainWindow(ShellViewModel viewModel,SettingsCoordinator settings,LocalizationService localization)
    {
        InitializeComponent();DataContext=viewModel;_settings=settings;_localization=localization;_localization.LanguageChanged+=OnLanguageChanged;var saved=settings.Current;Width=saved.WindowWidth;Height=saved.WindowHeight;
        if(saved.WindowLeft is{}left&&saved.WindowTop is{}top&&IsVisiblePosition(left,top)){WindowStartupLocation=WindowStartupLocation.Manual;Left=left;Top=top;}_startMaximized=saved.WindowMaximized;UpdateMaximizeRestoreButton();
        Closing+=OnClosing;StateChanged+=OnWindowStateChanged;
    }
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowSource=PresentationSource.FromVisual(this) as HwndSource;
        _windowSource?.AddHook(WindowProcedure);
        if(_startMaximized)WindowState=WindowState.Maximized;
    }
    protected override void OnClosed(EventArgs e){_localization.LanguageChanged-=OnLanguageChanged;_windowSource?.RemoveHook(WindowProcedure);_windowSource=null;base.OnClosed(e);}
    public void PrepareForExit(){_allowClose=true;}
    private void OnWindowStateChanged(object? sender,EventArgs e)=>UpdateMaximizeRestoreButton();
    private void MinimizeButton_Click(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
    private void MaximizeRestoreButton_Click(object sender,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
    private void CloseButton_Click(object sender,RoutedEventArgs e)=>Close();
    private void UpdateMaximizeRestoreButton()
    {
        if(MaximizeRestoreButton is null)return;
        MaximizeRestoreButton.Content=WindowState==WindowState.Maximized?"\uE923":"\uE922";
        MaximizeRestoreButton.ToolTip=_localization.Get(WindowState==WindowState.Maximized?"Window.Restore":"Window.Maximize");
    }
    private void OnLanguageChanged(object? sender,EventArgs e)=>UpdateMaximizeRestoreButton();
    private void OnClosing(object? sender,CancelEventArgs e)
    {
        SaveWindowState();if(_allowClose)return;e.Cancel=true;var requested=ExitRequested;if(requested is not null)_=requested();
    }
    private void SaveWindowState(){var bounds=RestoreBounds;_settings.Update(s=>s with{WindowWidth=bounds.Width,WindowHeight=bounds.Height,WindowLeft=bounds.Left,WindowTop=bounds.Top,WindowMaximized=WindowState==WindowState.Maximized});}
    private static bool IsVisiblePosition(double left,double top)=>left<SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-80&&top<SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight-80&&left>SystemParameters.VirtualScreenLeft-2000&&top>SystemParameters.VirtualScreenTop-2000;

    private static IntPtr WindowProcedure(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message!=WmGetMinMaxInfo)return IntPtr.Zero;
        var monitor=MonitorFromWindow(hwnd,MonitorDefaultToNearest);
        if(monitor==IntPtr.Zero)return IntPtr.Zero;
        var monitorInfo=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
        if(!GetMonitorInfo(monitor,ref monitorInfo))return IntPtr.Zero;
        var minMaxInfo=Marshal.PtrToStructure<MinMaxInfo>(lParam);
        minMaxInfo.MaxPosition.X=monitorInfo.WorkArea.Left-monitorInfo.MonitorArea.Left;
        minMaxInfo.MaxPosition.Y=monitorInfo.WorkArea.Top-monitorInfo.MonitorArea.Top;
        minMaxInfo.MaxSize.X=monitorInfo.WorkArea.Right-monitorInfo.WorkArea.Left;
        minMaxInfo.MaxSize.Y=monitorInfo.WorkArea.Bottom-monitorInfo.WorkArea.Top;
        Marshal.StructureToPtr(minMaxInfo,lParam,true);
        handled=true;
        return IntPtr.Zero;
    }

    [DllImport("user32.dll",CharSet=CharSet.Auto)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo monitorInfo);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint{public int X;public int Y;}
    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo{public NativePoint Reserved;public NativePoint MaxSize;public NativePoint MaxPosition;public NativePoint MinTrackSize;public NativePoint MaxTrackSize;}
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect{public int Left;public int Top;public int Right;public int Bottom;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Auto)]
    private struct MonitorInfo{public int Size;public NativeRect MonitorArea;public NativeRect WorkArea;public uint Flags;}
}
