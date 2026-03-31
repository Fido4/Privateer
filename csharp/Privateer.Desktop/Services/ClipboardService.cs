using System.Windows;
using System.Windows.Media.Imaging;

namespace Privateer.Desktop.Services;

public sealed class ClipboardService
{
    public void CopyImage(BitmapSource image)
    {
        Clipboard.SetImage(image);
    }
}
