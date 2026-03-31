using System;
using System.Drawing;
using System.Windows;
using Privateer.Desktop.Services;
using Privateer.Desktop.Windows;

namespace Privateer.Desktop;

public partial class App : Application
{
    private TrayIconService? _trayIconService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsService = new SettingsService();
        var settings = settingsService.Load();
        var themeManager = new ThemeManager();
        themeManager.ApplyTheme(this, settings.Theme);
        var hotkeyService = new CaptureHotkeyService();
        var launchOnStartupService = new LaunchOnStartupService();

        var mainWindow = new MainWindow(
            settings,
            settingsService,
            themeManager,
            new ScreenCaptureService(),
            new FileSaveService(),
            new ClipboardService(),
            hotkeyService,
            launchOnStartupService);

        MainWindow = mainWindow;

        var trayIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!)
            ?? SystemIcons.Application;

        _trayIconService = new TrayIconService(
            trayIcon,
            mainWindow.ShowFromBackground,
            mainWindow.ExitApplication);

        mainWindow.SetNotificationHandler(_trayIconService.ShowWarning);
        _trayIconService.ApplyTheme(themeManager.ResolvedTheme);
        themeManager.ThemeChanged += HandleThemeChanged;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconService?.Dispose();
        base.OnExit(e);
    }

    private void HandleThemeChanged(Models.AppTheme resolvedTheme)
    {
        _trayIconService?.ApplyTheme(resolvedTheme);
    }
}
