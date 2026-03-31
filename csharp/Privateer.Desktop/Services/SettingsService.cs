using System;
using System.IO;
using System.Text.Json;
using Privateer.Desktop.Models;

namespace Privateer.Desktop.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;

    public SettingsService()
    {
        var appFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrivateerDesktop");

        Directory.CreateDirectory(appFolder);
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            return Normalize(settings ?? new AppSettings());
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_settingsFilePath, json);
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.CustomCaptureHotkey = string.IsNullOrWhiteSpace(settings.CustomCaptureHotkey)
            ? "Ctrl+Shift+4"
            : settings.CustomCaptureHotkey.Trim();

        switch (settings.CaptureHotkey)
        {
            case CaptureHotkey.CtrlShift4:
                settings.CustomCaptureHotkey = "Ctrl+Shift+4";
                settings.CaptureHotkey = CaptureHotkey.Custom;
                break;
            case CaptureHotkey.CtrlShift5:
                settings.CustomCaptureHotkey = "Ctrl+Shift+5";
                settings.CaptureHotkey = CaptureHotkey.Custom;
                break;
            case CaptureHotkey.CtrlAltP:
                settings.CustomCaptureHotkey = "Ctrl+Alt+P";
                settings.CaptureHotkey = CaptureHotkey.Custom;
                break;
        }

        return settings;
    }
}
