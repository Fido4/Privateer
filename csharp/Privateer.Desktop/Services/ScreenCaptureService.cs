using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Privateer.Desktop.Services;

public sealed class ScreenCaptureService
{
    public Int32Rect GetVirtualScreenBounds()
    {
        return new Int32Rect(
            (int)SystemParameters.VirtualScreenLeft,
            (int)SystemParameters.VirtualScreenTop,
            (int)SystemParameters.VirtualScreenWidth,
            (int)SystemParameters.VirtualScreenHeight);
    }

    public BitmapSource CaptureVirtualScreen()
    {
        return CaptureRegion(GetVirtualScreenBounds());
    }

    public BitmapSource CropFromVirtualScreen(BitmapSource fullVirtualScreenImage, Int32Rect virtualScreenBounds, Int32Rect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "Capture region must be greater than zero.");
        }

        var relativeRegion = new Int32Rect(
            region.X - virtualScreenBounds.X,
            region.Y - virtualScreenBounds.Y,
            region.Width,
            region.Height);

        var cropped = new CroppedBitmap(fullVirtualScreenImage, relativeRegion);
        cropped.Freeze();
        return cropped;
    }

    public BitmapSource CaptureRegion(Int32Rect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "Capture region must be greater than zero.");
        }

        using var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(
            region.X,
            region.Y,
            0,
            0,
            new System.Drawing.Size(region.Width, region.Height),
            CopyPixelOperation.SourceCopy);

        var handle = bitmap.GetHbitmap();

        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);
}
