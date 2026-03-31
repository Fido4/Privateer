using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Privateer.Desktop.Windows;

public partial class CaptureOverlayWindow : Window
{
    private const int MagnifierDiameter = 192;
    private const int MagnifierSampleSize = 32;
    private const double MagnifierOffset = 28;

    private readonly BitmapSource _backgroundImage;
    private readonly Int32Rect _virtualScreenBounds;
    private Point? _dragStart;
    private Point _latestPointerPosition;
    private bool _hasPointerPosition;
    private bool _needsVisualRefresh;
    private int _lastMagnifierCropX = int.MinValue;
    private int _lastMagnifierCropY = int.MinValue;

    public CaptureOverlayWindow(BitmapSource backgroundImage, Int32Rect virtualScreenBounds)
    {
        InitializeComponent();

        _backgroundImage = backgroundImage;
        _virtualScreenBounds = virtualScreenBounds;
        Left = virtualScreenBounds.X;
        Top = virtualScreenBounds.Y;
        Width = virtualScreenBounds.Width;
        Height = virtualScreenBounds.Height;

        DimmedBackgroundImage.Source = backgroundImage;

        Loaded += (_, _) =>
        {
            Focus();
            Keyboard.Focus(this);
            var centerPoint = new Point(Width / 2, Height / 2);
            QueuePointerUpdate(centerPoint);
            ApplyQueuedPointerUpdate();
        };
        Closed += (_, _) => CompositionTarget.Rendering -= CompositionTarget_Rendering;
        CompositionTarget.Rendering += CompositionTarget_Rendering;
    }

    public Int32Rect? SelectedRegion { get; private set; }

    private void OverlayRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(OverlayRoot);
        OverlayRoot.CaptureMouse();
        QueuePointerUpdate(_dragStart.Value);
        ApplyQueuedPointerUpdate();
    }

    private void OverlayRoot_MouseMove(object sender, MouseEventArgs e)
    {
        QueuePointerUpdate(e.GetPosition(OverlayRoot));
    }

    private void OverlayRoot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }

        var position = e.GetPosition(OverlayRoot);
        QueuePointerUpdate(position);
        ApplyQueuedPointerUpdate();
        OverlayRoot.ReleaseMouseCapture();
        _dragStart = null;

        if (SelectedRegion is not Int32Rect region || region.Width < 6 || region.Height < 6)
        {
            ResetSelection();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OverlayRoot_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        CancelCapture();
    }

    private void CaptureOverlayWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        CancelCapture();
    }

    private void UpdateSelection(Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);

        SelectionBorder.Visibility = Visibility.Visible;
        SelectionInfoPill.Visibility = Visibility.Visible;

        Canvas.SetLeft(SelectionBorder, left);
        Canvas.SetTop(SelectionBorder, top);
        SelectionBorder.Width = width;
        SelectionBorder.Height = height;

        Canvas.SetLeft(SelectionInfoPill, left + 14);
        Canvas.SetTop(SelectionInfoPill, Math.Max(18, top - 48));
        SelectionInfoTextBlock.Text = $"{(int)Math.Round(width)} x {(int)Math.Round(height)}";
        SelectedRegion = new Int32Rect(
            (int)Math.Round(left + _virtualScreenBounds.X),
            (int)Math.Round(top + _virtualScreenBounds.Y),
            Math.Max(0, (int)Math.Round(width)),
            Math.Max(0, (int)Math.Round(height)));
    }

    private void ResetSelection()
    {
        SelectionBorder.Visibility = Visibility.Collapsed;
        SelectionInfoPill.Visibility = Visibility.Collapsed;
        SelectedRegion = null;
    }

    private void UpdateCrosshair(Point position)
    {
        CrosshairHorizontal.X1 = 0;
        CrosshairHorizontal.Y1 = position.Y;
        CrosshairHorizontal.X2 = Width;
        CrosshairHorizontal.Y2 = position.Y;

        CrosshairVertical.X1 = position.X;
        CrosshairVertical.Y1 = 0;
        CrosshairVertical.X2 = position.X;
        CrosshairVertical.Y2 = Height;
    }

    private void QueuePointerUpdate(Point position)
    {
        _latestPointerPosition = position;
        _hasPointerPosition = true;
        _needsVisualRefresh = true;
    }

    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        ApplyQueuedPointerUpdate();
    }

    private void ApplyQueuedPointerUpdate()
    {
        if (!_needsVisualRefresh || !_hasPointerPosition)
        {
            return;
        }

        _needsVisualRefresh = false;
        UpdateCrosshair(_latestPointerPosition);
        UpdateMagnifier(_latestPointerPosition);

        if (_dragStart is not null)
        {
            UpdateSelection(_dragStart.Value, _latestPointerPosition);
        }
    }

    private void UpdateMagnifier(Point position)
    {
        MagnifierHost.Visibility = Visibility.Visible;
        MagnifierLabel.Visibility = Visibility.Visible;

        var cropX = Clamp((int)Math.Round(position.X) - (MagnifierSampleSize / 2), 0, _backgroundImage.PixelWidth - MagnifierSampleSize);
        var cropY = Clamp((int)Math.Round(position.Y) - (MagnifierSampleSize / 2), 0, _backgroundImage.PixelHeight - MagnifierSampleSize);
        if (cropX != _lastMagnifierCropX || cropY != _lastMagnifierCropY)
        {
            _lastMagnifierCropX = cropX;
            _lastMagnifierCropY = cropY;

            var cropped = new CroppedBitmap(_backgroundImage, new Int32Rect(cropX, cropY, MagnifierSampleSize, MagnifierSampleSize));
            var transformed = new TransformedBitmap(cropped, new ScaleTransform(
                MagnifierDiameter / (double)MagnifierSampleSize,
                MagnifierDiameter / (double)MagnifierSampleSize));
            transformed.Freeze();
            MagnifierImage.Source = transformed;
        }

        var desiredX = position.X + MagnifierOffset;
        var desiredY = position.Y + MagnifierOffset;

        if (desiredX + MagnifierDiameter > Width - 8)
        {
            desiredX = position.X - MagnifierDiameter - MagnifierOffset;
        }

        if (desiredY + MagnifierDiameter > Height - 8)
        {
            desiredY = position.Y - MagnifierDiameter - MagnifierOffset;
        }

        desiredX = Math.Max(8, desiredX);
        desiredY = Math.Max(8, desiredY);

        Canvas.SetLeft(MagnifierHost, desiredX);
        Canvas.SetTop(MagnifierHost, desiredY);

        Canvas.SetLeft(MagnifierLabel, desiredX);
        Canvas.SetTop(MagnifierLabel, Math.Max(8, desiredY - 30));
        MagnifierLabelTextBlock.Text = $"{(int)Math.Round(position.X + _virtualScreenBounds.X)}, {(int)Math.Round(position.Y + _virtualScreenBounds.Y)}";
    }

    private static int Clamp(int value, int minimum, int maximum)
    {
        if (maximum < minimum)
        {
            return minimum;
        }

        if (value < minimum)
        {
            return minimum;
        }

        return value > maximum ? maximum : value;
    }

    private void CancelCapture()
    {
        ResetSelection();
        DialogResult = false;
        Close();
    }
}
