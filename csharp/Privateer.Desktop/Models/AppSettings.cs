using System;
using System.IO;

namespace Privateer.Desktop.Models;

public enum AppTheme
{
    System,
    Light,
    Dark,
    TanSepia,
    BrownSepia,
    GreenSepia
}

public enum CaptureHotkey
{
    CtrlShift4,
    CtrlShift5,
    CtrlAltP,
    PrintScreen,
    Custom
}

public sealed class AppSettings
{
    public string PreferredSaveFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "Privateer");

    public string PreferredFileNamePrefix { get; set; } = "Capture";

    public AppTheme Theme { get; set; } = AppTheme.System;

    public bool ShowQuickActionsAfterCapture { get; set; } = true;

    public CaptureHotkey CaptureHotkey { get; set; } = CaptureHotkey.PrintScreen;

    public string CustomCaptureHotkey { get; set; } = "Ctrl+Shift+4";

    public bool LaunchOnStartup { get; set; }
}
