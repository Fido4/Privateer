using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Privateer.Desktop.Models;

public sealed record CaptureResult(BitmapSource Image, Int32Rect Region, DateTimeOffset CapturedAt);
