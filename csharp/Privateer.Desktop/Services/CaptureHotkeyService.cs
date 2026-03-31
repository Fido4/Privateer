using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Privateer.Desktop.Models;

namespace Privateer.Desktop.Services;

public sealed class CaptureHotkeyService : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const int ErrorHotkeyAlreadyRegistered = 1409;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly int _hotkeyId = Guid.NewGuid().GetHashCode();
    private HwndSource? _source;
    private IntPtr _handle;
    private bool _isRegistered;
    private CaptureHotkey _currentHotkey = CaptureHotkey.PrintScreen;
    private string _customHotkey = "Ctrl+Shift+4";
    private Action? _onHotkeyPressed;

    public void Attach(Window window, Action onHotkeyPressed)
    {
        _onHotkeyPressed = onHotkeyPressed;

        var ensuredHandle = new WindowInteropHelper(window).EnsureHandle();
        if (ensuredHandle != IntPtr.Zero)
        {
            var ensuredSource = HwndSource.FromHwnd(ensuredHandle);
            if (ensuredSource is not null)
            {
                AttachSource(ensuredSource);
                return;
            }
        }

        if (PresentationSource.FromVisual(window) is HwndSource source)
        {
            AttachSource(source);
            return;
        }

        void HandleSourceInitialized(object? sender, EventArgs args)
        {
            window.SourceInitialized -= HandleSourceInitialized;
            if (PresentationSource.FromVisual(window) is HwndSource initializedSource)
            {
                AttachSource(initializedSource);
            }
        }

        window.SourceInitialized += HandleSourceInitialized;
    }

    public HotkeyRegistrationResult UpdateHotkey(CaptureHotkey hotkey, string? customHotkey = null)
    {
        _currentHotkey = hotkey;
        if (!string.IsNullOrWhiteSpace(customHotkey))
        {
            _customHotkey = customHotkey.Trim();
        }

        if (_source is not null)
        {
            return RegisterCurrentHotkey();
        }

        return HotkeyRegistrationResult.Pending(GetDisplayText(_currentHotkey, _customHotkey));
    }

    public void Dispose()
    {
        UnregisterCurrentHotkey();

        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }

    private void AttachSource(HwndSource source)
    {
        if (_source == source)
        {
            RegisterCurrentHotkey();
            return;
        }

        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
        }

        _source = source;
        _handle = source.Handle;
        _source.AddHook(WndProc);
        RegisterCurrentHotkey();
    }

    private HotkeyRegistrationResult RegisterCurrentHotkey()
    {
        var displayText = GetDisplayText(_currentHotkey, _customHotkey);
        if (_handle == IntPtr.Zero)
        {
            return HotkeyRegistrationResult.Pending(displayText);
        }

        UnregisterCurrentHotkey();

        var (modifiers, virtualKey) = GetHotkeyDefinition(_currentHotkey, _customHotkey);
        if (virtualKey == 0)
        {
            return HotkeyRegistrationResult.Invalid(
                displayText,
                $"Capture hotkey \"{displayText}\" could not be registered. Choose Print Screen or record a different custom key combination.");
        }

        _isRegistered = RegisterHotKey(_handle, _hotkeyId, modifiers | ModNoRepeat, virtualKey);
        if (_isRegistered)
        {
            return HotkeyRegistrationResult.Success(displayText);
        }

        var error = Marshal.GetLastWin32Error();
        if (error == ErrorHotkeyAlreadyRegistered)
        {
            return HotkeyRegistrationResult.Conflict(
                displayText,
                $"Capture hotkey \"{displayText}\" is already in use by another app. Choose a different hotkey in Preferences.");
        }

        return HotkeyRegistrationResult.Failure(
            displayText,
            $"Capture hotkey \"{displayText}\" could not be registered. Windows reported error {error}.");
    }

    private void UnregisterCurrentHotkey()
    {
        if (!_isRegistered || _handle == IntPtr.Zero)
        {
            return;
        }

        UnregisterHotKey(_handle, _hotkeyId);
        _isRegistered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && wParam.ToInt32() == _hotkeyId)
        {
            _onHotkeyPressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public static string GetDisplayText(CaptureHotkey hotkey, string? customHotkey = null)
    {
        return hotkey switch
        {
            CaptureHotkey.PrintScreen => "Print Screen",
            CaptureHotkey.Custom => string.IsNullOrWhiteSpace(customHotkey) ? "Ctrl+Shift+4" : customHotkey,
            _ => "Print Screen"
        };
    }

    public static bool TryBuildCustomHotkey(ModifierKeys modifiers, Key key, Key systemKey, out string hotkeyText)
    {
        hotkeyText = string.Empty;

        var normalizedKey = key == Key.System ? systemKey : key;
        if (!TryNormalizeKey(normalizedKey, out var normalized))
        {
            return false;
        }

        var parts = new System.Collections.Generic.List<string>();
        if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            parts.Add("Alt");
        }

        if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            parts.Add("Shift");
        }

        if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
        {
            parts.Add("Win");
        }

        parts.Add(normalized);
        hotkeyText = string.Join("+", parts);
        return true;
    }

    public static bool IsModifierOnlyKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
               Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or
               Key.Clear or Key.OemClear;
    }

    private static (uint Modifiers, uint VirtualKey) GetHotkeyDefinition(CaptureHotkey hotkey, string customHotkey)
    {
        return hotkey switch
        {
            CaptureHotkey.PrintScreen => (0, 0x2C),
            CaptureHotkey.Custom => TryParseCustomHotkey(customHotkey, out var definition)
                ? definition
                : (0, 0),
            _ => (0, 0x2C)
        };
    }

    private static bool TryParseCustomHotkey(string hotkey, out (uint Modifiers, uint VirtualKey) definition)
    {
        definition = default;
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return false;
        }

        uint modifiers = 0;
        uint virtualKey = 0;
        var segments = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var segment in segments)
        {
            switch (segment.ToUpperInvariant())
            {
                case "CTRL":
                    modifiers |= ModControl;
                    continue;
                case "ALT":
                    modifiers |= ModAlt;
                    continue;
                case "SHIFT":
                    modifiers |= ModShift;
                    continue;
                case "WIN":
                    modifiers |= ModWin;
                    continue;
            }

            if (!TryParseKeySegment(segment, out virtualKey))
            {
                return false;
            }
        }

        if (virtualKey == 0)
        {
            return false;
        }

        definition = (modifiers, virtualKey);
        return true;
    }

    private static bool TryParseKeySegment(string segment, out uint virtualKey)
    {
        virtualKey = 0;
        var normalized = segment.Trim().ToUpperInvariant();

        if (normalized.Length == 1)
        {
            var ch = normalized[0];
            if (ch is >= 'A' and <= 'Z' || ch is >= '0' and <= '9')
            {
                virtualKey = ch;
                return true;
            }
        }

        if (normalized == "PRINT SCREEN" || normalized == "PRTSC" || normalized == "PRTSCN")
        {
            virtualKey = 0x2C;
            return true;
        }

        if (normalized.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(normalized[1..], out var functionNumber) &&
            functionNumber is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + functionNumber - 1);
            return true;
        }

        if (Enum.TryParse<Key>(normalized, true, out var parsedKey))
        {
            virtualKey = (uint)KeyInterop.VirtualKeyFromKey(parsedKey);
            return virtualKey != 0;
        }

        return false;
    }

    private static bool TryNormalizeKey(Key key, out string normalized)
    {
        normalized = string.Empty;
        if (IsModifierOnlyKey(key))
        {
            return false;
        }

        if (key is >= Key.A and <= Key.Z)
        {
            normalized = key.ToString().ToUpperInvariant();
            return true;
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            normalized = ((char)('0' + (key - Key.D0))).ToString();
            return true;
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            normalized = $"NumPad{key - Key.NumPad0}";
            return true;
        }

        if (key is >= Key.F1 and <= Key.F24)
        {
            normalized = key.ToString().ToUpperInvariant();
            return true;
        }

        normalized = key switch
        {
            Key.PrintScreen => "Print Screen",
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Enter => "Enter",
            Key.Escape => "Esc",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.OemPlus => "OemPlus",
            Key.OemMinus => "OemMinus",
            Key.OemComma => "OemComma",
            Key.OemPeriod => "OemPeriod",
            Key.OemQuestion => "OemQuestion",
            Key.OemSemicolon => "OemSemicolon",
            Key.OemQuotes => "OemQuotes",
            Key.OemOpenBrackets => "OemOpenBrackets",
            Key.OemCloseBrackets => "OemCloseBrackets",
            Key.OemPipe => "OemPipe",
            Key.OemTilde => "OemTilde",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(normalized);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

public readonly record struct HotkeyRegistrationResult(
    bool IsRegistered,
    bool IsConflict,
    bool IsPending,
    string DisplayText,
    string? Message)
{
    public static HotkeyRegistrationResult Success(string displayText) =>
        new(true, false, false, displayText, null);

    public static HotkeyRegistrationResult Pending(string displayText) =>
        new(true, false, true, displayText, null);

    public static HotkeyRegistrationResult Conflict(string displayText, string message) =>
        new(false, true, false, displayText, message);

    public static HotkeyRegistrationResult Invalid(string displayText, string message) =>
        new(false, false, false, displayText, message);

    public static HotkeyRegistrationResult Failure(string displayText, string message) =>
        new(false, false, false, displayText, message);
}
