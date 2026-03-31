using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Privateer.Desktop.Interop;

public static class DwmHelper
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmSystemBackdropType = 38;
    private const int CornerPreferenceRound = 2;
    private const int BackdropMainWindow = 2;

    public static void ApplyModernWindowStyle(Window window, bool useDarkMode)
    {
        void Apply()
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var darkModeValue = useDarkMode ? 1 : 0;
            var cornerValue = CornerPreferenceRound;
            var backdropValue = BackdropMainWindow;

            DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref darkModeValue, Marshal.SizeOf<int>());
            DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref cornerValue, Marshal.SizeOf<int>());
            DwmSetWindowAttribute(handle, DwmSystemBackdropType, ref backdropValue, Marshal.SizeOf<int>());
        }

        if (PresentationSource.FromVisual(window) is not null)
        {
            Apply();
            return;
        }

        void HandleSourceInitialized(object? sender, EventArgs args)
        {
            window.SourceInitialized -= HandleSourceInitialized;
            Apply();
        }

        window.SourceInitialized += HandleSourceInitialized;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
