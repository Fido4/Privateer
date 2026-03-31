using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Privateer.Desktop.Models;

namespace Privateer.Desktop.Services;

public sealed class FileSaveService
{
    private static readonly Regex InvalidNameCharacters = new(
        $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]",
        RegexOptions.Compiled);

    public string SaveToPreferredLocation(BitmapSource image, AppSettings settings, DateTimeOffset capturedAt)
    {
        var targetPath = BuildPreferredPath(settings, capturedAt);
        SaveBitmap(image, targetPath);
        return targetPath;
    }

    public string? SaveAs(Window? owner, BitmapSource image, AppSettings settings, DateTimeOffset capturedAt)
    {
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".png",
            Filter = "PNG Image (*.png)|*.png",
            InitialDirectory = EnsureDirectory(settings.PreferredSaveFolder),
            FileName = BuildPreferredFileName(settings, capturedAt)
        };

        var accepted = owner is null
            ? dialog.ShowDialog()
            : dialog.ShowDialog(owner);

        if (accepted != true)
        {
            return null;
        }

        SaveBitmap(image, dialog.FileName);

        settings.PreferredSaveFolder = Path.GetDirectoryName(dialog.FileName) ?? settings.PreferredSaveFolder;
        settings.PreferredFileNamePrefix = Path.GetFileNameWithoutExtension(dialog.FileName);
        return dialog.FileName;
    }

    public string BuildPreferredPath(AppSettings settings, DateTimeOffset capturedAt)
    {
        var folder = EnsureDirectory(settings.PreferredSaveFolder);
        var fileName = BuildPreferredFileName(settings, capturedAt);
        var path = Path.Combine(folder, fileName);

        if (!File.Exists(path))
        {
            return path;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var index = 2; index < 5000; index++)
        {
            var candidate = Path.Combine(folder, $"{baseName}_{index}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(folder, $"{baseName}_{Guid.NewGuid():N}{extension}");
    }

    public string GetPreferredPathPreview(AppSettings settings, DateTimeOffset capturedAt)
    {
        return BuildPreferredPath(settings, capturedAt);
    }

    public string BuildPreferredFileName(AppSettings settings, DateTimeOffset capturedAt)
    {
        var prefix = string.IsNullOrWhiteSpace(settings.PreferredFileNamePrefix)
            ? "Capture"
            : InvalidNameCharacters.Replace(settings.PreferredFileNamePrefix.Trim(), "_");

        return $"{prefix}_{capturedAt:yyyy-MM-dd_HH-mm-ss}.png";
    }

    private static string EnsureDirectory(string? folder)
    {
        var resolved = string.IsNullOrWhiteSpace(folder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Privateer")
            : folder;

        Directory.CreateDirectory(resolved);
        return resolved;
    }

    private static void SaveBitmap(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
