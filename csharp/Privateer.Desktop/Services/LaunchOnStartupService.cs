using System;
using Microsoft.Win32;

namespace Privateer.Desktop.Services;

public sealed class LaunchOnStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "PrivateerDesktop";

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            if (runKey is null)
            {
                return;
            }

            if (!enabled)
            {
                runKey.DeleteValue(AppName, throwOnMissingValue: false);
                return;
            }

            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                return;
            }

            runKey.SetValue(AppName, $"\"{processPath}\"");
        }
        catch
        {
            // Ignore registry access failures so startup preference support never blocks app launch.
        }
    }
}
