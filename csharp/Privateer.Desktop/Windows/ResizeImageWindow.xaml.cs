using System;
using System.Windows;
using System.Windows.Controls;

namespace Privateer.Desktop.Windows;

public partial class ResizeImageWindow : Window
{
    private readonly double _aspectRatio;
    private bool _suppressUpdates;

    public ResizeImageWindow(int currentWidth, int currentHeight)
    {
        InitializeComponent();
        _aspectRatio = currentWidth / (double)currentHeight;
        WidthTextBox.Text = currentWidth.ToString();
        HeightTextBox.Text = currentHeight.ToString();
    }

    public int TargetWidth { get; private set; }

    public int TargetHeight { get; private set; }

    private void DimensionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressUpdates || MaintainAspectRatioCheckBox.IsChecked != true)
        {
            return;
        }

        if (sender == WidthTextBox && int.TryParse(WidthTextBox.Text, out var width) && width > 0)
        {
            _suppressUpdates = true;
            HeightTextBox.Text = Math.Max(1, (int)Math.Round(width / _aspectRatio)).ToString();
            _suppressUpdates = false;
        }
        else if (sender == HeightTextBox && int.TryParse(HeightTextBox.Text, out var height) && height > 0)
        {
            _suppressUpdates = true;
            WidthTextBox.Text = Math.Max(1, (int)Math.Round(height * _aspectRatio)).ToString();
            _suppressUpdates = false;
        }
    }

    private void MaintainAspectRatioCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        DimensionTextBox_TextChanged(WidthTextBox, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WidthTextBox.Text, out var width) || width <= 0 ||
            !int.TryParse(HeightTextBox.Text, out var height) || height <= 0)
        {
            MessageBox.Show(this, "Enter valid positive width and height values.", "Resize Image", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TargetWidth = width;
        TargetHeight = height;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
