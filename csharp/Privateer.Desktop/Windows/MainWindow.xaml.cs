using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Privateer.Desktop.Interop;
using Privateer.Desktop.Models;
using Privateer.Desktop.Services;

namespace Privateer.Desktop.Windows;

public partial class MainWindow : Window
{
    private const double DefaultMainWindowHeight = 840;
    private const double DefaultMainWindowMinHeight = 840;
    private const double CustomHotkeyMainWindowHeight = 930;
    private const double CustomHotkeyMainWindowMinHeight = 930;

    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly ThemeManager _themeManager;
    private readonly ScreenCaptureService _screenCaptureService;
    private readonly FileSaveService _fileSaveService;
    private readonly ClipboardService _clipboardService;
    private readonly CaptureHotkeyService _captureHotkeyService;
    private readonly LaunchOnStartupService _launchOnStartupService;
    private readonly List<EditorWindow> _editorWindows = [];
    private CaptureResult? _currentCapture;
    private bool _allowWindowClose;
    private bool _autoResizedForCustomHotkey;
    private bool _isApplyingTheme;
    private bool _isCaptureInProgress;
    private string? _lastHotkeyWarningMessage;
    private Action<string, string>? _notifyUser;

    public MainWindow(
        AppSettings settings,
        SettingsService settingsService,
        ThemeManager themeManager,
        ScreenCaptureService screenCaptureService,
        FileSaveService fileSaveService,
        ClipboardService clipboardService,
        CaptureHotkeyService captureHotkeyService,
        LaunchOnStartupService launchOnStartupService)
    {
        InitializeComponent();

        _settings = settings;
        _settingsService = settingsService;
        _themeManager = themeManager;
        _screenCaptureService = screenCaptureService;
        _fileSaveService = fileSaveService;
        _clipboardService = clipboardService;
        _captureHotkeyService = captureHotkeyService;
        _launchOnStartupService = launchOnStartupService;

        Loaded += (_, _) => _themeManager.ApplyWindowTheme(this);
        Closed += (_, _) => _captureHotkeyService.Dispose();
        Closing += MainWindow_Closing;
        ApplySettingsToForm();
        _captureHotkeyService.Attach(this, HandleCaptureHotkeyPressed);
        ApplyPreferenceIntegrations();
        UpdateSavePreview();
        UpdateCaptureUi();
    }

    private void ApplySettingsToForm()
    {
        SaveFolderTextBox.Text = _settings.PreferredSaveFolder;
        FileNamePrefixTextBox.Text = _settings.PreferredFileNamePrefix;
        CustomCaptureHotkeyTextBox.Text = CaptureHotkeyService.GetDisplayText(CaptureHotkey.Custom, _settings.CustomCaptureHotkey);
        LaunchOnStartupCheckBox.IsChecked = _settings.LaunchOnStartup;
        QuickActionsCheckBox.IsChecked = _settings.ShowQuickActionsAfterCapture;

        foreach (ComboBoxItem item in CaptureHotkeyComboBox.Items)
        {
            if (string.Equals(item.Tag?.ToString(), _settings.CaptureHotkey.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                CaptureHotkeyComboBox.SelectedItem = item;
                break;
            }
        }

        UpdateCustomHotkeyUi();

        foreach (ComboBoxItem item in ThemeComboBox.Items)
        {
            if (string.Equals(item.Tag?.ToString(), _settings.Theme.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                ThemeComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private async void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        await BeginCaptureAsync();
    }

    private async Task BeginCaptureAsync()
    {
        if (_isCaptureInProgress)
        {
            return;
        }

        _isCaptureInProgress = true;
        SyncSettingsFromForm();
        PersistSettings();

        try
        {
            var virtualScreen = _screenCaptureService.GetVirtualScreenBounds();
            var background = _screenCaptureService.CaptureVirtualScreen();
            var overlay = new CaptureOverlayWindow(background, virtualScreen);
            if (IsVisible)
            {
                overlay.Owner = this;
            }

            _themeManager.ApplyWindowTheme(overlay);

            var captureAccepted = overlay.ShowDialog() == true &&
                                  overlay.SelectedRegion is Int32Rect region &&
                                  region.Width > 0 &&
                                  region.Height > 0;

            if (!captureAccepted)
            {
                SetStatus("Capture canceled.");
                return;
            }

            var selectedRegion = overlay.SelectedRegion!.Value;
            _currentCapture = new CaptureResult(
                _screenCaptureService.CropFromVirtualScreen(background, virtualScreen, selectedRegion),
                selectedRegion,
                DateTimeOffset.Now);

            UpdateCaptureUi();
            SetStatus($"Captured {selectedRegion.Width} x {selectedRegion.Height}.");

            if (_settings.ShowQuickActionsAfterCapture)
            {
                ShowQuickActions();
                return;
            }

            OpenEditor();
        }
        catch (Exception ex)
        {
            SetStatus($"Capture failed: {ex.Message}");
        }
        finally
        {
            _isCaptureInProgress = false;
        }
    }

    private void ShowQuickActions()
    {
        if (_currentCapture is null)
        {
            return;
        }

        var dialog = new QuickActionsWindow(_currentCapture, _fileSaveService.GetPreferredPathPreview(_settings, _currentCapture.CapturedAt));
        if (IsVisible)
        {
            dialog.Owner = this;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        _themeManager.ApplyWindowTheme(dialog);
        dialog.ShowDialog();

        switch (dialog.SelectedAction)
        {
            case CaptureQuickAction.Save:
                SaveCurrentCapture();
                break;
            case CaptureQuickAction.SaveAs:
                SaveCurrentCaptureAs();
                break;
            case CaptureQuickAction.Copy:
                CopyCurrentCapture();
                break;
            case CaptureQuickAction.OpenEditor:
                OpenEditor();
                break;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentCapture();
    }

    private void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentCaptureAs();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        CopyCurrentCapture();
    }

    private void OpenEditorButton_Click(object sender, RoutedEventArgs e)
    {
        OpenEditor();
    }

    private void SaveCurrentCapture()
    {
        if (_currentCapture is null)
        {
            return;
        }

        try
        {
            SyncSettingsFromForm();
            var path = _fileSaveService.SaveToPreferredLocation(_currentCapture.Image, _settings, _currentCapture.CapturedAt);
            PersistSettings();
            UpdateSavePreview();
            SetStatus($"Saved capture to {path}");
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
        }
    }

    private void SaveCurrentCaptureAs()
    {
        SaveCurrentCaptureAs(IsVisible ? this : null);
    }

    private void SaveCurrentCaptureAs(Window? owner)
    {
        if (_currentCapture is null)
        {
            return;
        }

        try
        {
            SyncSettingsFromForm();
            var path = _fileSaveService.SaveAs(owner, _currentCapture.Image, _settings, _currentCapture.CapturedAt);
            if (string.IsNullOrWhiteSpace(path))
            {
                SetStatus("Save As canceled.");
                return;
            }

            ApplySettingsToForm();
            PersistSettings();
            UpdateSavePreview();
            SetStatus($"Saved capture to {path}");
        }
        catch (Exception ex)
        {
            SetStatus($"Save As failed: {ex.Message}");
        }
    }

    private void CopyCurrentCapture()
    {
        if (_currentCapture is null)
        {
            return;
        }

        try
        {
            _clipboardService.CopyImage(_currentCapture.Image);
            SetStatus("Capture copied to the clipboard.");
        }
        catch (Exception ex)
        {
            SetStatus($"Clipboard copy failed: {ex.Message}");
        }
    }

    private void OpenEditor()
    {
        if (_currentCapture is null)
        {
            return;
        }

        try
        {
            var editor = new EditorWindow(
                _currentCapture,
                _settings,
                _settingsService,
                _themeManager,
                _fileSaveService,
                _clipboardService);

            RegisterEditorWindow(editor);
            _themeManager.ApplyWindowTheme(editor);
            _autoResizedForCustomHotkey = false;

            if (IsVisible)
            {
                Hide();
            }

            editor.Show();
            editor.Activate();
            editor.Focus();
            SetStatus("Editor opened.");
        }
        catch (Exception ex)
        {
            SetStatus($"Editor failed to open: {ex.Message}");
        }
    }

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose preferred save folder",
            InitialDirectory = SaveFolderTextBox.Text
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        SaveFolderTextBox.Text = dialog.FolderName;
        SavePreferences();
    }

    private void SavePreferencesButton_Click(object sender, RoutedEventArgs e)
    {
        SavePreferences();
    }

    private void SavePreferences()
    {
        SyncSettingsFromForm();
        PersistSettings();
        ApplyPreferenceIntegrations();
        UpdateSavePreview();
        SetStatus("Preferences saved.");
    }

    private async void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _isApplyingTheme)
        {
            return;
        }

        SyncSettingsFromForm();
        PersistSettings();
        ThemeComboBox.IsDropDownOpen = false;

        _isApplyingTheme = true;

        try
        {
            await Dispatcher.InvokeAsync(
                () => _themeManager.ApplyTheme(Application.Current, _settings.Theme),
                DispatcherPriority.ContextIdle);
        }
        finally
        {
            _isApplyingTheme = false;
        }

        UpdateSavePreview();
        SetStatus($"Theme set to {_settings.Theme}.");
    }

    private void PreferencesInputChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSavePreview();
    }

    private void CaptureHotkeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CustomCaptureHotkeyPanel is null)
        {
            return;
        }

        UpdateCustomHotkeyUi();
    }

    private void CustomCaptureHotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (CaptureHotkeyComboBox.SelectedItem is not ComboBoxItem selectedHotkeyItem ||
            !string.Equals(selectedHotkeyItem.Tag?.ToString(), CaptureHotkey.Custom.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (CaptureHotkeyService.TryBuildCustomHotkey(Keyboard.Modifiers, e.Key, e.SystemKey, out var hotkeyText))
        {
            CustomCaptureHotkeyTextBox.Text = hotkeyText;
            SetStatus($"Custom hotkey set to {hotkeyText}.");
        }

        e.Handled = true;
    }

    private void QuickActionsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        SyncSettingsFromForm();
        PersistSettings();
    }

    private void PersistSettings()
    {
        _settingsService.Save(_settings);
    }

    private void SyncSettingsFromForm()
    {
        _settings.PreferredSaveFolder = SaveFolderTextBox.Text.Trim();
        _settings.PreferredFileNamePrefix = FileNamePrefixTextBox.Text.Trim();
        _settings.LaunchOnStartup = LaunchOnStartupCheckBox.IsChecked == true;
        _settings.ShowQuickActionsAfterCapture = QuickActionsCheckBox.IsChecked == true;

        if (CaptureHotkeyComboBox.SelectedItem is ComboBoxItem selectedHotkeyItem &&
            Enum.TryParse<CaptureHotkey>(selectedHotkeyItem.Tag?.ToString(), out var hotkey))
        {
            _settings.CaptureHotkey = hotkey;
        }

        _settings.CustomCaptureHotkey = string.IsNullOrWhiteSpace(CustomCaptureHotkeyTextBox.Text)
            ? "Ctrl+Shift+4"
            : CustomCaptureHotkeyTextBox.Text.Trim();

        if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem &&
            Enum.TryParse<AppTheme>(selectedItem.Tag?.ToString(), out var theme))
        {
            _settings.Theme = theme;
        }
    }

    private void UpdateSavePreview()
    {
        var previewSettings = new AppSettings
        {
            PreferredSaveFolder = SaveFolderTextBox.Text.Trim(),
            PreferredFileNamePrefix = FileNamePrefixTextBox.Text.Trim(),
            Theme = _settings.Theme,
            CaptureHotkey = _settings.CaptureHotkey,
            CustomCaptureHotkey = CustomCaptureHotkeyTextBox?.Text?.Trim() ?? _settings.CustomCaptureHotkey,
            LaunchOnStartup = LaunchOnStartupCheckBox?.IsChecked == true,
            ShowQuickActionsAfterCapture = QuickActionsCheckBox.IsChecked == true
        };

        SavePreviewTextBlock.Text = $"Next Save: {_fileSaveService.GetPreferredPathPreview(previewSettings, DateTimeOffset.Now)}";
    }

    private void UpdateCaptureUi()
    {
        var hasCapture = _currentCapture is not null;
        SaveButton.IsEnabled = hasCapture;
        SaveAsButton.IsEnabled = hasCapture;
        CopyButton.IsEnabled = hasCapture;
        OpenEditorButton.IsEnabled = hasCapture;

        PreviewPlaceholderPanel.Visibility = hasCapture ? Visibility.Collapsed : Visibility.Visible;
        PreviewImage.Visibility = hasCapture ? Visibility.Visible : Visibility.Collapsed;

        if (!hasCapture)
        {
            CaptureSummaryTextBlock.Text = "Waiting for a capture";
            CaptureDetailsTextBlock.Text = "Select a region to preview it here.";
            PreviewImage.Source = null;
            return;
        }

        PreviewImage.Source = _currentCapture!.Image;
        CaptureSummaryTextBlock.Text = $"{_currentCapture.Region.Width} x {_currentCapture.Region.Height} capture ready";
        CaptureDetailsTextBlock.Text = $"Captured at {_currentCapture.CapturedAt:MMM d, yyyy h:mm:ss tt}. Use Save for your default path or open the editor for markup.";
    }

    private void SetStatus(string message)
    {
        _ = message;
    }

    private void ApplyPreferenceIntegrations()
    {
        var hotkeyResult = _captureHotkeyService.UpdateHotkey(_settings.CaptureHotkey, _settings.CustomCaptureHotkey);
        _launchOnStartupService.SetEnabled(_settings.LaunchOnStartup);
        HandleHotkeyRegistrationResult(hotkeyResult);
    }

    public void SetNotificationHandler(Action<string, string> notifyUser)
    {
        _notifyUser = notifyUser;

        if (!string.IsNullOrWhiteSpace(_lastHotkeyWarningMessage))
        {
            _notifyUser("Capture Hotkey Unavailable", _lastHotkeyWarningMessage);
        }
    }

    public void ShowFromBackground()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Topmost = true;
        Topmost = false;
        Activate();
        Focus();
    }

    public void ExitApplication()
    {
        _allowWindowClose = true;
        _autoResizedForCustomHotkey = false;

        foreach (var editorWindow in _editorWindows.ToList())
        {
            editorWindow.Close();
        }

        Close();
    }

    private void UpdateCustomHotkeyUi()
    {
        var isCustom = CaptureHotkeyComboBox.SelectedItem is ComboBoxItem selectedHotkeyItem &&
                       string.Equals(selectedHotkeyItem.Tag?.ToString(), CaptureHotkey.Custom.ToString(), StringComparison.OrdinalIgnoreCase);

        CustomCaptureHotkeyPanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
        CustomCaptureHotkeyTextBox.IsEnabled = isCustom;

        if (isCustom)
        {
            MinHeight = CustomHotkeyMainWindowMinHeight;

            if (Height < CustomHotkeyMainWindowHeight)
            {
                Height = CustomHotkeyMainWindowHeight;
                _autoResizedForCustomHotkey = true;
            }
        }
        else
        {
            MinHeight = DefaultMainWindowMinHeight;

            if (_autoResizedForCustomHotkey || Height < DefaultMainWindowHeight)
            {
                Height = DefaultMainWindowHeight;
            }

            _autoResizedForCustomHotkey = false;
        }
    }

    private async void HandleCaptureHotkeyPressed()
    {
        if (_isCaptureInProgress)
        {
            return;
        }

        await BeginCaptureAsync();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowWindowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void HandleHotkeyRegistrationResult(HotkeyRegistrationResult result)
    {
        if (result.IsRegistered)
        {
            _lastHotkeyWarningMessage = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(result.Message))
        {
            return;
        }

        if (string.Equals(_lastHotkeyWarningMessage, result.Message, StringComparison.Ordinal))
        {
            return;
        }

        _lastHotkeyWarningMessage = result.Message;
        SetStatus(result.Message);
        _notifyUser?.Invoke("Capture Hotkey Unavailable", result.Message);
    }

    private void RegisterEditorWindow(EditorWindow editor)
    {
        _editorWindows.Add(editor);
        editor.Closed += (_, _) => _editorWindows.Remove(editor);
    }
}
