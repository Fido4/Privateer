using System.Windows;
using Privateer.Desktop.Models;

namespace Privateer.Desktop.Windows;

public partial class QuickActionsWindow : Window
{
    public QuickActionsWindow(CaptureResult capture, string preferredPathPreview)
    {
        InitializeComponent();
        PreviewImage.Source = capture.Image;
        PathPreviewTextBlock.Text = $"Default Save target: {preferredPathPreview}";
    }

    public CaptureQuickAction SelectedAction { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAction(CaptureQuickAction.Save);
    }

    private void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAction(CaptureQuickAction.SaveAs);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAction(CaptureQuickAction.Copy);
    }

    private void OpenEditorButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAction(CaptureQuickAction.OpenEditor);
    }

    private void CloseWithAction(CaptureQuickAction action)
    {
        SelectedAction = action;
        Close();
    }
}
