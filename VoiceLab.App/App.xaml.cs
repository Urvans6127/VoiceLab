using VoiceLab.App.Services;
using VoiceLab.App.ViewModels;
using VoiceLab.Audio;
using VoiceLab.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace VoiceLab.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private int _shutdownStarted;
    private int _exceptionHandled;

    protected override async void OnStartup(StartupEventArgs e)
    {
        var startupTimer = System.Diagnostics.Stopwatch.StartNew();
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        _services = new ServiceCollection()
            .AddSingleton<AudioEngine>()
            .AddSingleton<PresetStore>()
            .AddSingleton<ISettingsStore, JsonSettingsStore>()
            .AddSingleton<SettingsCoordinator>()
            .AddSingleton<LocalizationService>()
            .AddSingleton<PresetTransferService>()
            .AddSingleton<FileDialogService>()
            .AddSingleton<FolderLauncher>()
            .AddSingleton(_ => new FileLogger())
            .AddSingleton<MainViewModel>()
            .AddSingleton<PresetsViewModel>().AddSingleton<RecordingViewModel>()
            .AddSingleton<DevicesViewModel>().AddSingleton<SettingsViewModel>().AddSingleton<AboutViewModel>()
            .AddSingleton<ShellViewModel>().AddSingleton<MainWindow>()
            .BuildServiceProvider();

        var settings = _services.GetRequiredService<SettingsCoordinator>();
        await settings.InitializeAsync();
        var localization=_services.GetRequiredService<LocalizationService>();
        localization.SetLanguage(settings.Current.Language);
        if(!string.Equals(settings.Current.Language,localization.CurrentLanguage,StringComparison.OrdinalIgnoreCase))
            settings.Update(current=>current with{Language=localization.CurrentLanguage});
        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.ExitRequested += RequestExitAsync;
        window.Show();
        _services.GetRequiredService<FileLogger>().LogDiagnostic("Application startup", startupTimer.Elapsed);
    }

    public async Task RequestExitAsync()
    {
        var shutdownTimer = System.Diagnostics.Stopwatch.StartNew();
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0) return;
        var services = _services;
        if (services is null) { Shutdown(); return; }
        if (MainWindow is MainWindow window) window.PrepareForExit();
        var componentTimer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            componentTimer.Restart();
            await services.GetRequiredService<AudioEngine>().StopAsync();
            services.GetRequiredService<FileLogger>().LogDiagnostic("Application audio shutdown", componentTimer.Elapsed);
            componentTimer.Restart();
            await services.GetRequiredService<SettingsCoordinator>().FlushAsync();
            services.GetRequiredService<FileLogger>().LogDiagnostic("Settings flush", componentTimer.Elapsed);
        }
        catch (Exception ex)
        {
            services.GetRequiredService<FileLogger>().Log("Shutdown finalization error; a recording may be incomplete", ex);
        }
        finally
        {
            services.GetRequiredService<FileLogger>().LogDiagnostic("Dispatcher shutdown requested", shutdownTimer.Elapsed);
            Shutdown();
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        if (MainWindow is MainWindow window) window.PrepareForExit();
        base.OnSessionEnding(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _services?.GetService<FileLogger>()?.Log("Unhandled application exception", e.Exception);
        e.Handled = true;
        if (Interlocked.Exchange(ref _exceptionHandled, 1) == 0)
        {
            var localization=_services?.GetService<LocalizationService>();
            System.Windows.MessageBox.Show(localization?.Get("Error.Unhandled")??"Error.Unhandled",localization?.Get("Error.UnhandledTitle")??"Error.UnhandledTitle",MessageBoxButton.OK,MessageBoxImage.Error);
        }
        _ = RequestExitAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        if (_services is not null)
        {
            try { _services.GetRequiredService<AudioEngine>().StopAsync().GetAwaiter().GetResult(); } catch { }
            try { _services.GetRequiredService<SettingsCoordinator>().FlushAsync().GetAwaiter().GetResult(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            _services.Dispose();
            _services = null;
        }
        base.OnExit(e);
    }
}
