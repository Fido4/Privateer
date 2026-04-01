using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Privateer.Desktop.Models;
using Privateer.Desktop.Services;

namespace Privateer.Desktop.Windows;

public partial class EditorWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly ThemeManager _themeManager;
    private readonly FileSaveService _fileSaveService;
    private readonly ClipboardService _clipboardService;
    private readonly List<AnnotationRecord> _annotations = [];
    private readonly List<EditorSnapshot> _history = [];
    private readonly Dictionary<EditorTool, Button> _toolButtons = [];
    private readonly List<Button> _colorButtons = [];
    private const string DefaultTextAnnotation = "Note";
    private const string DefaultSpeechBubbleText = "Comment";
    private const double MinimumSelectionLength = 8;
    private const double MinimumTextAnnotationWidth = 120;
    private const double MinimumTextAnnotationHeight = 42;
    private const int MaximumTextAnnotationCharacters = MaximumSpeechBubbleCharacters;
    private const int MaximumTextAnnotationCharactersPerLine = 50;
    private const double DefaultSpeechBubbleWidth = 180;
    private const double DefaultSpeechBubbleHeight = 54;
    private const double MinimumSpeechBubbleTextWidth = 96;
    private const double MaximumSpeechBubbleWidth = 420;
    private const double MaximumSpeechBubbleHeight = 150;
    private const int MaximumSpeechBubbleCharacters = 280;
    private const double SpeechBubbleHorizontalInsets = 32;
    private const double SpeechBubbleVerticalInsets = 24;

    private readonly BitmapSource _originalImage;
    private BitmapSource _workingImage;
    private bool _suppressHistory;
    private bool _isCommittingTextEdit;
    private bool _isEditingNewTextAnnotation;
    private bool _suppressTextEditorTextChanged;
    private Point? _dragStart;
    private FrameworkElement? _previewElement;
    private FrameworkElement? _activeTextEditorHost;
    private TextBox? _activeTextEditor;
    private AnnotationRecord? _editingTextAnnotation;
    private string? _editingOriginalText;
    private string _lastValidSpeechBubbleText = string.Empty;
    private int _historyIndex = -1;
    private int _nextCounter = 1;
    private string _selectedColorHex = "#FF00A6FF";
    private EditorTool _currentTool = EditorTool.Pen;

    public EditorWindow(
        CaptureResult capture,
        AppSettings settings,
        SettingsService settingsService,
        ThemeManager themeManager,
        FileSaveService fileSaveService,
        ClipboardService clipboardService)
    {
        InitializeComponent();

        _originalImage = capture.Image;
        _workingImage = _originalImage;
        _settings = settings;
        _settingsService = settingsService;
        _themeManager = themeManager;
        _fileSaveService = fileSaveService;
        _clipboardService = clipboardService;

        Loaded += (_, _) => _themeManager.ApplyWindowTheme(this);

        RegisterButtons();
        SetWorkingImage(_workingImage);

        InkLayer.StrokeCollected += InkLayer_StrokeCollected;
        InkLayer.Strokes.StrokesChanged += Strokes_StrokesChanged;

        SelectColor(_selectedColorHex);
        SetCurrentTool(EditorTool.Pen);
        CommitSnapshot();
        SetStatus($"Editing {capture.Region.Width} x {capture.Region.Height} capture.");
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            SaveEditedImage();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S)
        {
            SaveEditedImageAs();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
        {
            CopyEditedImage();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
        {
            Undo();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
        {
            Redo();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void RegisterButtons()
    {
        _toolButtons[EditorTool.Pen] = PenToolButton;
        _toolButtons[EditorTool.Highlighter] = HighlighterToolButton;
        _toolButtons[EditorTool.Rectangle] = RectangleToolButton;
        _toolButtons[EditorTool.Ellipse] = EllipseToolButton;
        _toolButtons[EditorTool.Line] = LineToolButton;
        _toolButtons[EditorTool.Arrow] = ArrowToolButton;
        _toolButtons[EditorTool.Text] = TextToolButton;
        _toolButtons[EditorTool.SpeechBubble] = SpeechBubbleToolButton;
        _toolButtons[EditorTool.Counter] = CounterToolButton;
        _toolButtons[EditorTool.Obfuscate] = ObfuscateToolButton;

        _colorButtons.AddRange([
            ColorSkyButton,
            ColorCoralButton,
            ColorAmberButton,
            ColorEmeraldButton,
            ColorVioletButton,
            ColorWhiteButton
        ]);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveEditedImage();

    private void SaveAsButton_Click(object sender, RoutedEventArgs e) => SaveEditedImageAs();

    private void CopyButton_Click(object sender, RoutedEventArgs e) => CopyEditedImage();

    private void UndoButton_Click(object sender, RoutedEventArgs e) => Undo();

    private void RedoButton_Click(object sender, RoutedEventArgs e) => Redo();

    private void CloseMenuItem_Click(object sender, RoutedEventArgs e) => Close();

    private void PreferencesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.ShowFromBackground();
            SetStatus("Preferences window opened.");
            return;
        }

        var preferencesWindow = Owner ?? Application.Current.MainWindow;
        if (preferencesWindow is null)
        {
            return;
        }

        if (!preferencesWindow.IsVisible)
        {
            preferencesWindow.Show();
        }

        if (preferencesWindow.WindowState == WindowState.Minimized)
        {
            preferencesWindow.WindowState = WindowState.Normal;
        }

        preferencesWindow.Topmost = true;
        preferencesWindow.Topmost = false;
        preferencesWindow.Activate();
        preferencesWindow.Focus();
        SetStatus("Preferences window opened.");
    }

    private void ToolSelector_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag?.ToString();
        if (!Enum.TryParse<EditorTool>(tag, out var tool))
        {
            return;
        }

        SetCurrentTool(tool);
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        var colorHex = (sender as FrameworkElement)?.Tag?.ToString();
        if (!string.IsNullOrWhiteSpace(colorHex))
        {
            SelectColor(colorHex);
        }
    }

    private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsEditorSurfaceReady())
        {
            return;
        }

        UpdateDrawingAttributes();
    }

    private void RotateLeftMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var oldWidth = _workingImage.PixelWidth;
        var oldHeight = _workingImage.PixelHeight;
        var transformMatrix = new Matrix(0, -1, 1, 0, 0, oldWidth);

        ApplyImageTransformation(
            image => RotateBitmap(image, 270),
            annotation => TransformAnnotation(annotation, transformMatrix),
            strokes => TransformStrokes(strokes, transformMatrix, false),
            "Rotated image left.");
    }

    private void RotateRightMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var oldWidth = _workingImage.PixelWidth;
        var oldHeight = _workingImage.PixelHeight;
        var transformMatrix = new Matrix(0, 1, -1, 0, oldHeight, 0);

        ApplyImageTransformation(
            image => RotateBitmap(image, 90),
            annotation => TransformAnnotation(annotation, transformMatrix),
            strokes => TransformStrokes(strokes, transformMatrix, false),
            "Rotated image right.");
    }

    private void ResizeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ResizeImageWindow(_workingImage.PixelWidth, _workingImage.PixelHeight)
        {
            Owner = this
        };

        _themeManager.ApplyWindowTheme(dialog);

        if (dialog.ShowDialog() == true)
        {
            var scaleX = dialog.TargetWidth / (double)_workingImage.PixelWidth;
            var scaleY = dialog.TargetHeight / (double)_workingImage.PixelHeight;
            var transformMatrix = new Matrix(scaleX, 0, 0, scaleY, 0, 0);

            ApplyImageTransformation(
                image => ResizeBitmap(image, dialog.TargetWidth, dialog.TargetHeight),
                annotation => TransformAnnotation(annotation, transformMatrix),
                strokes => TransformStrokes(strokes, transformMatrix, true),
                $"Resized image to {dialog.TargetWidth} x {dialog.TargetHeight}.");
        }
    }

    private void InteractionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(InteractionCanvas);
        if (ShouldCommitActiveTextEditOnClick(position))
        {
            CommitPendingTextEdit();
            e.Handled = true;
            return;
        }

        if (_currentTool is EditorTool.Pen or EditorTool.Eraser)
        {
            return;
        }

        if (_currentTool == EditorTool.Text)
        {
            CommitPendingTextEdit();

            if (TryBeginTextAnnotationEdit(position, AnnotationKind.Text))
            {
                return;
            }

            AddTextAnnotation(position);
            return;
        }

        if (_currentTool == EditorTool.SpeechBubble)
        {
            CommitPendingTextEdit();

            if (TryBeginTextAnnotationEdit(position, AnnotationKind.SpeechBubble))
            {
                return;
            }

            AddSpeechBubble(position);
            return;
        }

        CommitPendingTextEdit();

        if (_currentTool == EditorTool.Counter)
        {
            AddCounter(position);
            return;
        }

        _dragStart = position;
        InteractionCanvas.CaptureMouse();
        RenderPreviewAnnotation(position, position);
    }

    private void InteractionCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not null)
        {
            RenderPreviewAnnotation(_dragStart.Value, e.GetPosition(InteractionCanvas));
        }
    }

    private void InteractionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }

        var end = e.GetPosition(InteractionCanvas);
        InteractionCanvas.ReleaseMouseCapture();
        RemovePreview();

        if (_currentTool == EditorTool.Obfuscate)
        {
            ApplyObfuscation(_dragStart.Value, end);
            _dragStart = null;
            return;
        }

        var record = CreateAnnotationRecord(_dragStart.Value, end);
        _dragStart = null;

        if (record is null)
        {
            return;
        }

        _annotations.Add(record);
        RenderAnnotations();
        CommitSnapshot();
        SetStatus($"{record.Kind} added.");
    }

    private void ClearAnnotationsButton_Click(object sender, RoutedEventArgs e)
    {
        CancelPendingTextEdit();
        RemovePreview();
        SetWorkingImage(_originalImage);
        _annotations.Clear();
        ReplaceInkStrokes([]);
        _nextCounter = 1;
        RenderAnnotations();
        CommitSnapshot();
        SetStatus("Cleared all editor changes.");
    }

    private void InkLayer_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e) => CommitSnapshot();

    private void Strokes_StrokesChanged(object? sender, StrokeCollectionChangedEventArgs e)
    {
        if (!_suppressHistory && _currentTool == EditorTool.Eraser && (e.Added.Count > 0 || e.Removed.Count > 0))
        {
            CommitSnapshot();
        }
    }

    private void SetCurrentTool(EditorTool tool)
    {
        CommitPendingTextEdit();
        _currentTool = tool;

        foreach (var (registeredTool, button) in _toolButtons)
        {
            if (registeredTool == tool)
            {
                button.SetResourceReference(BackgroundProperty, "AccentBrush");
                button.SetResourceReference(BorderBrushProperty, "AccentBrush");
                button.SetResourceReference(ForegroundProperty, "AccentTextBrush");
            }
            else
            {
                button.SetResourceReference(BackgroundProperty, "GhostButtonBrush");
                button.SetResourceReference(BorderBrushProperty, "GhostButtonBorderBrush");
                button.ClearValue(ForegroundProperty);
            }
        }

        UpdateDrawingAttributes();
        UpdateToolMode();
    }

    private void SelectColor(string colorHex)
    {
        _selectedColorHex = colorHex;

        foreach (var button in _colorButtons)
        {
            var isSelected = string.Equals(button.Tag?.ToString(), colorHex, StringComparison.OrdinalIgnoreCase);

            if (!isSelected)
            {
                button.SetResourceReference(BorderBrushProperty, "GhostButtonBorderBrush");
            }
            else if (string.Equals(button.Tag?.ToString(), "#FFFFFFFF", StringComparison.OrdinalIgnoreCase))
            {
                button.SetResourceReference(BorderBrushProperty, "PrimaryTextBrush");
            }
            else
            {
                button.BorderBrush = button.Background;
            }

            button.BorderThickness = isSelected
                ? new Thickness(4)
                : new Thickness(1.5);
        }

        UpdateDrawingAttributes();
    }

    private void UpdateDrawingAttributes()
    {
        if (!IsEditorSurfaceReady())
        {
            return;
        }

        InkLayer.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = (Color)ColorConverter.ConvertFromString(_selectedColorHex),
            Width = ThicknessSlider.Value,
            Height = ThicknessSlider.Value,
            FitToCurve = true,
            IgnorePressure = true,
            IsHighlighter = false
        };
    }

    private void UpdateToolMode()
    {
        switch (_currentTool)
        {
            case EditorTool.Pen:
                InkLayer.EditingMode = InkCanvasEditingMode.Ink;
                InteractionCanvas.IsHitTestVisible = false;
                ToolHintTextBlock.Text = "Freehand drawing tool.";
                break;
            case EditorTool.Highlighter:
                InkLayer.EditingMode = InkCanvasEditingMode.None;
                InteractionCanvas.IsHitTestVisible = true;
                ToolHintTextBlock.Text = "Drag to apply a translucent highlight region.";
                break;
            case EditorTool.Eraser:
                InkLayer.EditingMode = InkCanvasEditingMode.EraseByStroke;
                InteractionCanvas.IsHitTestVisible = false;
                ToolHintTextBlock.Text = "Erase freehand strokes.";
                break;
            case EditorTool.Rectangle:
                InkLayer.EditingMode = InkCanvasEditingMode.None;
                InteractionCanvas.IsHitTestVisible = true;
                ToolHintTextBlock.Text = "Drag to draw a rectangle.";
                break;
            case EditorTool.Ellipse:
                InkLayer.EditingMode = InkCanvasEditingMode.None;
                InteractionCanvas.IsHitTestVisible = true;
                ToolHintTextBlock.Text = "Drag to draw an ellipse.";
                break;
            case EditorTool.Line:
                InkLayer.EditingMode = InkCanvasEditingMode.None;
                InteractionCanvas.IsHitTestVisible = true;
                ToolHintTextBlock.Text = "Drag to draw a straight line.";
                break;
            case EditorTool.Arrow:
                InkLayer.EditingMode = InkCanvasEditingMode.None;
                InteractionCanvas.IsHitTestVisible = true;
                ToolHintTextBlock.Text = "Drag to draw an arrow.";
                break;
            case EditorTool.Text:
                InkLayer.EditingMode = InkCanvasEditingMode.None;
                InteractionCanvas.IsHitTestVisible = true;
                ToolHintTextBlock.Text = "Click to place a text box.";
                break;
            case EditorTool.SpeechBubble:
                InkLayer.EditingMode = InkCanvasEditingMode.None;
                InteractionCanvas.IsHitTestVisible = true;
                ToolHintTextBlock.Text = "Click to place a speech bubble.";
                break;
            case EditorTool.Counter:
                InkLayer.EditingMode = InkCanvasEditingMode.None;
                InteractionCanvas.IsHitTestVisible = true;
                ToolHintTextBlock.Text = "Click to place an incrementing counter badge.";
                break;
            case EditorTool.Obfuscate:
                InkLayer.EditingMode = InkCanvasEditingMode.None;
                InteractionCanvas.IsHitTestVisible = true;
                ToolHintTextBlock.Text = "Drag to pixelate a region. Release to apply it immediately.";
                break;
        }
    }

    private void AddTextAnnotation(Point position)
    {
        var record = new AnnotationRecord
        {
            Kind = AnnotationKind.Text,
            StartX = position.X,
            StartY = position.Y,
            Text = DefaultTextAnnotation,
            ColorHex = _selectedColorHex,
            FontSize = Math.Max(16, ThicknessSlider.Value * 3)
        };

        RefreshTextAnnotationLayout(record);
        _annotations.Add(record);
        RenderAnnotations();
        BeginTextAnnotationEdit(record, true);
    }

    private void AddSpeechBubble(Point position)
    {
        var record = new AnnotationRecord
        {
            Kind = AnnotationKind.SpeechBubble,
            StartX = position.X,
            StartY = position.Y,
            Text = DefaultSpeechBubbleText,
            ColorHex = _selectedColorHex,
            FontSize = Math.Max(15, ThicknessSlider.Value * 2.6),
            Width = DefaultSpeechBubbleWidth,
            Height = DefaultSpeechBubbleHeight
        };

        RefreshTextAnnotationLayout(record);
        _annotations.Add(record);
        RenderAnnotations();
        BeginTextAnnotationEdit(record, true);
    }

    private void AddCounter(Point position)
    {
        var record = new AnnotationRecord
        {
            Kind = AnnotationKind.Counter,
            StartX = position.X,
            StartY = position.Y,
            Text = _nextCounter.ToString(),
            ColorHex = _selectedColorHex,
            FontSize = 18,
            Width = 34,
            Height = 34
        };

        _nextCounter++;
        _annotations.Add(record);
        RenderAnnotations();
        CommitSnapshot();
        SetStatus("Counter added.");
    }

    private void RenderPreviewAnnotation(Point start, Point end)
    {
        RemovePreview();

        if (_currentTool == EditorTool.Obfuscate)
        {
            _previewElement = BuildObfuscationPreview(start, end);
            if (_previewElement is not null)
            {
                AnnotationCanvas.Children.Add(_previewElement);
            }

            return;
        }

        var previewRecord = CreateAnnotationRecord(start, end);
        if (previewRecord is null)
        {
            return;
        }

        _previewElement = BuildAnnotationElement(previewRecord, true);
        AnnotationCanvas.Children.Add(_previewElement);
    }

    private void RemovePreview()
    {
        if (_previewElement is null)
        {
            return;
        }

        AnnotationCanvas.Children.Remove(_previewElement);
        _previewElement = null;
    }

    private AnnotationRecord? CreateAnnotationRecord(Point start, Point end)
    {
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        if (width < 4 && height < 4 && _currentTool != EditorTool.Line && _currentTool != EditorTool.Arrow)
        {
            return null;
        }

        return _currentTool switch
        {
            EditorTool.Highlighter => CreateHighlightRecord(start, end),
            EditorTool.Rectangle => CreateShapeRecord(AnnotationKind.Rectangle, start, end),
            EditorTool.Ellipse => CreateShapeRecord(AnnotationKind.Ellipse, start, end),
            EditorTool.Line => CreateShapeRecord(AnnotationKind.Line, start, end),
            EditorTool.Arrow => CreateShapeRecord(AnnotationKind.Arrow, start, end),
            _ => null
        };
    }

    private AnnotationRecord CreateHighlightRecord(Point start, Point end)
    {
        var record = CreateShapeRecord(AnnotationKind.Highlight, start, end);
        record.Opacity = GetHighlightOpacity();
        return record;
    }

    private AnnotationRecord CreateShapeRecord(AnnotationKind kind, Point start, Point end)
    {
        return new AnnotationRecord
        {
            Kind = kind,
            StartX = start.X,
            StartY = start.Y,
            EndX = end.X,
            EndY = end.Y,
            ColorHex = _selectedColorHex,
            Thickness = ThicknessSlider.Value,
            Opacity = 1
        };
    }

    private void RenderAnnotations()
    {
        AnnotationCanvas.Children.Clear();

        foreach (var annotation in _annotations)
        {
            if (ReferenceEquals(annotation, _editingTextAnnotation))
            {
                continue;
            }

            AnnotationCanvas.Children.Add(BuildAnnotationElement(annotation, false));
        }
    }

    private FrameworkElement BuildAnnotationElement(AnnotationRecord annotation, bool isPreview)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(annotation.ColorHex));
        brush.Opacity = isPreview ? 0.75 : annotation.Opacity;

        return annotation.Kind switch
        {
            AnnotationKind.Highlight => BuildHighlight(annotation, brush),
            AnnotationKind.Rectangle => BuildRectangle(annotation, brush),
            AnnotationKind.Ellipse => BuildEllipse(annotation, brush),
            AnnotationKind.Line => BuildLine(annotation, brush),
            AnnotationKind.Arrow => BuildArrow(annotation, brush),
            AnnotationKind.Text => BuildText(annotation, brush),
            AnnotationKind.SpeechBubble => BuildSpeechBubble(annotation, brush),
            AnnotationKind.Counter => BuildCounter(annotation, brush),
            _ => throw new InvalidOperationException("Unsupported annotation kind.")
        };
    }

    private static FrameworkElement BuildHighlight(AnnotationRecord annotation, Brush brush)
    {
        var (left, top, width, height) = GetBounds(annotation);
        var fill = new Border
        {
            Width = width,
            Height = height,
            Background = brush
        };

        Canvas.SetLeft(fill, left);
        Canvas.SetTop(fill, top);
        return fill;
    }

    private static FrameworkElement BuildRectangle(AnnotationRecord annotation, Brush brush)
    {
        var (left, top, width, height) = GetBounds(annotation);
        var border = new Border
        {
            Width = width,
            Height = height,
            BorderBrush = brush,
            BorderThickness = new Thickness(annotation.Thickness),
            Background = Brushes.Transparent
        };

        Canvas.SetLeft(border, left);
        Canvas.SetTop(border, top);
        return border;
    }

    private static FrameworkElement BuildEllipse(AnnotationRecord annotation, Brush brush)
    {
        var (left, top, width, height) = GetBounds(annotation);
        var ellipse = new Ellipse
        {
            Width = width,
            Height = height,
            Stroke = brush,
            StrokeThickness = annotation.Thickness,
            Fill = Brushes.Transparent
        };

        Canvas.SetLeft(ellipse, left);
        Canvas.SetTop(ellipse, top);
        return ellipse;
    }

    private static FrameworkElement BuildLine(AnnotationRecord annotation, Brush brush)
    {
        return new Line
        {
            X1 = annotation.StartX,
            Y1 = annotation.StartY,
            X2 = annotation.EndX,
            Y2 = annotation.EndY,
            Stroke = brush,
            StrokeThickness = annotation.Thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
    }

    private static Line BuildArrowShaft(Point start, Point end, Brush brush, double thickness)
    {
        return new Line
        {
            X1 = start.X,
            Y1 = start.Y,
            X2 = end.X,
            Y2 = end.Y,
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
    }

    private FrameworkElement BuildArrow(AnnotationRecord annotation, Brush brush)
    {
        var start = new Point(annotation.StartX, annotation.StartY);
        var end = new Point(annotation.EndX, annotation.EndY);
        var headSize = Math.Max(8, annotation.Thickness * 4.4);
        var shaftEnd = GetArrowShaftEnd(start, end, headSize * 0.82);
        var canvas = new Canvas
        {
            Width = AnnotationCanvas.Width,
            Height = AnnotationCanvas.Height,
            IsHitTestVisible = false
        };

        canvas.Children.Add(BuildArrowShaft(start, shaftEnd, brush, annotation.Thickness));
        canvas.Children.Add(new Polygon
        {
            Fill = brush,
            Points = BuildArrowHead(start, end, headSize)
        });

        return canvas;
    }

    private static FrameworkElement BuildText(AnnotationRecord annotation, Brush brush)
    {
        var container = new Border
        {
            Width = annotation.Width <= 0 ? MinimumTextAnnotationWidth : annotation.Width,
            Height = annotation.Height <= 0 ? MinimumTextAnnotationHeight : annotation.Height,
            Background = new SolidColorBrush(Color.FromArgb(190, 18, 18, 18)),
            BorderBrush = brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 8, 12, 8)
        };

        container.Child = new TextBlock
        {
            Text = annotation.Text ?? "Note",
            Foreground = brush,
            FontFamily = new FontFamily("Consolas"),
            FontSize = annotation.FontSize,
            TextWrapping = TextWrapping.Wrap
        };

        Canvas.SetLeft(container, annotation.StartX);
        Canvas.SetTop(container, annotation.StartY);
        return container;
    }

    private static FrameworkElement BuildSpeechBubble(AnnotationRecord annotation, Brush brush)
    {
        var width = annotation.Width <= 0 ? DefaultSpeechBubbleWidth : annotation.Width;
        var height = annotation.Height <= 0 ? DefaultSpeechBubbleHeight : annotation.Height;

        var canvas = new Canvas
        {
            Width = width + 24,
            Height = height + 24,
            IsHitTestVisible = false
        };

        var bubble = new Border
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20)),
            BorderBrush = brush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14, 10, 14, 10),
            Child = new TextBlock
            {
                Text = annotation.Text ?? "Comment",
                Foreground = brush,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = annotation.FontSize
            }
        };

        var tail = new Polygon
        {
            Fill = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20)),
            Stroke = brush,
            StrokeThickness = 2,
            Points = [new Point(28, height), new Point(52, height), new Point(18, height + 20)]
        };

        canvas.Children.Add(bubble);
        canvas.Children.Add(tail);
        Canvas.SetLeft(canvas, annotation.StartX);
        Canvas.SetTop(canvas, annotation.StartY);
        return canvas;
    }

    private static FrameworkElement BuildCounter(AnnotationRecord annotation, Brush brush)
    {
        var size = annotation.Width <= 0 ? 34 : annotation.Width;
        var grid = new Grid
        {
            Width = size,
            Height = size,
            IsHitTestVisible = false
        };

        grid.Children.Add(new Ellipse
        {
            Fill = brush,
            Stroke = Brushes.White,
            StrokeThickness = 1.5
        });

        grid.Children.Add(new TextBlock
        {
            Text = annotation.Text ?? "1",
            Foreground = Brushes.Black,
            FontFamily = new FontFamily("Consolas"),
            FontSize = annotation.FontSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        Canvas.SetLeft(grid, annotation.StartX);
        Canvas.SetTop(grid, annotation.StartY);
        return grid;
    }

    private FrameworkElement? BuildObfuscationPreview(Point start, Point end)
    {
        if (!TryGetSelectionBounds(start, end, MinimumSelectionLength, out var bounds))
        {
            return null;
        }

        var preview = new Border
        {
            Width = bounds.Width,
            Height = bounds.Height,
            Background = new SolidColorBrush(Color.FromArgb(92, 18, 18, 18)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(215, 255, 255, 255)),
            BorderThickness = new Thickness(1)
        };

        Canvas.SetLeft(preview, bounds.Left);
        Canvas.SetTop(preview, bounds.Top);
        return preview;
    }

    private void ApplyObfuscation(Point start, Point end)
    {
        if (!TryGetSelectionBounds(start, end, MinimumSelectionLength, out var bounds))
        {
            SetStatus("Obfuscation canceled because the selection was too small.");
            return;
        }

        ApplyImageTransformation(
            image => PixelateBitmapRegion(image, bounds, GetObfuscationBlockSize()),
            annotation => annotation.Clone(),
            CloneStrokeCollection,
            $"Obfuscated region {(int)Math.Round(bounds.Width)} x {(int)Math.Round(bounds.Height)}.");
    }

    private void ApplyImageTransformation(
        Func<BitmapSource, BitmapSource> imageTransform,
        Func<AnnotationRecord, AnnotationRecord> annotationTransform,
        Func<StrokeCollection, StrokeCollection> strokeTransform,
        string statusMessage)
    {
        try
        {
            CommitPendingTextEdit();
            RemovePreview();
            var transformedImage = imageTransform(_workingImage);
            var transformedAnnotations = _annotations.Select(annotationTransform).ToList();
            var transformedStrokes = strokeTransform(InkLayer.Strokes);

            _annotations.Clear();
            _annotations.AddRange(transformedAnnotations);
            ReplaceInkStrokes(transformedStrokes);
            SetWorkingImage(transformedImage);
            RenderAnnotations();
            CommitSnapshot();
            SetStatus(statusMessage);
        }
        catch (Exception ex)
        {
            SetStatus($"Image operation failed: {ex.Message}");
        }
    }

    private void SetWorkingImage(BitmapSource image)
    {
        _workingImage = image;
        BaseImage.Source = image;
        BaseImage.Width = image.PixelWidth;
        BaseImage.Height = image.PixelHeight;

        AnnotationCanvas.Width = image.PixelWidth;
        AnnotationCanvas.Height = image.PixelHeight;
        InkLayer.Width = image.PixelWidth;
        InkLayer.Height = image.PixelHeight;
        InteractionCanvas.Width = image.PixelWidth;
        InteractionCanvas.Height = image.PixelHeight;
    }

    private void ReplaceInkStrokes(StrokeCollection strokes)
    {
        InkLayer.Strokes.StrokesChanged -= Strokes_StrokesChanged;
        InkLayer.Strokes = strokes;
        InkLayer.Strokes.StrokesChanged += Strokes_StrokesChanged;
    }

    private void CommitSnapshot()
    {
        if (_suppressHistory)
        {
            return;
        }

        while (_history.Count - 1 > _historyIndex)
        {
            _history.RemoveAt(_history.Count - 1);
        }

        _history.Add(new EditorSnapshot(_workingImage, InkLayer.Strokes, _annotations, _nextCounter));
        _historyIndex = _history.Count - 1;
    }

    private void Undo()
    {
        CommitPendingTextEdit();

        if (_historyIndex <= 0)
        {
            return;
        }

        _historyIndex--;
        RestoreSnapshot(_history[_historyIndex]);
        SetStatus("Undid the last editor action.");
    }

    private void Redo()
    {
        CommitPendingTextEdit();

        if (_historyIndex >= _history.Count - 1)
        {
            return;
        }

        _historyIndex++;
        RestoreSnapshot(_history[_historyIndex]);
        SetStatus("Redid the editor action.");
    }

    private void RestoreSnapshot(EditorSnapshot snapshot)
    {
        _suppressHistory = true;

        SetWorkingImage(snapshot.BaseImage);
        ReplaceInkStrokes(CloneStrokeCollection(snapshot.Strokes));

        _annotations.Clear();
        _annotations.AddRange(snapshot.Annotations.Select(annotation => annotation.Clone()));
        _nextCounter = snapshot.NextCounter;
        RenderAnnotations();
        RemovePreview();

        _suppressHistory = false;
    }

    private void SaveEditedImage()
    {
        try
        {
            CommitPendingTextEdit();
            var rendered = RenderEditedImage();
            var path = _fileSaveService.SaveToPreferredLocation(rendered, _settings, DateTimeOffset.Now);
            _settingsService.Save(_settings);
            SetStatus($"Saved edited capture to {path}");
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
        }
    }

    private void SaveEditedImageAs()
    {
        try
        {
            CommitPendingTextEdit();
            var rendered = RenderEditedImage();
            var path = _fileSaveService.SaveAs(this, rendered, _settings, DateTimeOffset.Now);
            if (string.IsNullOrWhiteSpace(path))
            {
                SetStatus("Save As canceled.");
                return;
            }

            _settingsService.Save(_settings);
            SetStatus($"Saved edited capture to {path}");
        }
        catch (Exception ex)
        {
            SetStatus($"Save As failed: {ex.Message}");
        }
    }

    private void CopyEditedImage()
    {
        try
        {
            CommitPendingTextEdit();
            _clipboardService.CopyImage(RenderEditedImage());
            SetStatus("Edited image copied to the clipboard.");
        }
        catch (Exception ex)
        {
            SetStatus($"Clipboard copy failed: {ex.Message}");
        }
    }

    private BitmapSource RenderEditedImage()
    {
        EditorSurface.Measure(new Size(BaseImage.Width, BaseImage.Height));
        EditorSurface.Arrange(new Rect(new Size(BaseImage.Width, BaseImage.Height)));
        EditorSurface.UpdateLayout();

        var width = Math.Max(1, (int)Math.Ceiling(BaseImage.Width));
        var height = Math.Max(1, (int)Math.Ceiling(BaseImage.Height));

        var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        renderTarget.Render(EditorSurface);
        renderTarget.Freeze();
        return renderTarget;
    }

    private static BitmapSource RotateBitmap(BitmapSource source, double angle)
    {
        var transformed = new TransformedBitmap(source, new RotateTransform(angle));
        transformed.Freeze();
        return transformed;
    }

    private static BitmapSource ResizeBitmap(BitmapSource source, int targetWidth, int targetHeight)
    {
        var scaleX = targetWidth / (double)source.PixelWidth;
        var scaleY = targetHeight / (double)source.PixelHeight;
        var transformed = new TransformedBitmap(source, new ScaleTransform(scaleX, scaleY));
        transformed.Freeze();
        return transformed;
    }

    private static AnnotationRecord TransformAnnotation(AnnotationRecord annotation, Matrix transformMatrix)
    {
        var transformed = annotation.Clone();

        switch (annotation.Kind)
        {
            case AnnotationKind.Line:
            case AnnotationKind.Arrow:
                var transformedStart = transformMatrix.Transform(new Point(annotation.StartX, annotation.StartY));
                var transformedEnd = transformMatrix.Transform(new Point(annotation.EndX, annotation.EndY));
                transformed.StartX = transformedStart.X;
                transformed.StartY = transformedStart.Y;
                transformed.EndX = transformedEnd.X;
                transformed.EndY = transformedEnd.Y;
                transformed.Thickness = ScaleScalar(annotation.Thickness, transformMatrix);
                break;

            case AnnotationKind.Highlight:
            case AnnotationKind.Rectangle:
            case AnnotationKind.Ellipse:
                var transformedBounds = TransformBounds(GetBoundsRect(annotation), transformMatrix);
                transformed.StartX = transformedBounds.Left;
                transformed.StartY = transformedBounds.Top;
                transformed.EndX = transformedBounds.Right;
                transformed.EndY = transformedBounds.Bottom;
                transformed.Thickness = ScaleScalar(annotation.Thickness, transformMatrix);
                break;

            case AnnotationKind.Text:
                var transformedTextOrigin = transformMatrix.Transform(new Point(annotation.StartX, annotation.StartY));
                transformed.StartX = transformedTextOrigin.X;
                transformed.StartY = transformedTextOrigin.Y;
                transformed.FontSize = ScaleScalar(annotation.FontSize, transformMatrix);
                break;

            case AnnotationKind.SpeechBubble:
                var speechBounds = TransformBounds(
                    new Rect(annotation.StartX, annotation.StartY, annotation.Width + 24, annotation.Height + 24),
                    transformMatrix);
                transformed.StartX = speechBounds.Left;
                transformed.StartY = speechBounds.Top;
                transformed.Width = Math.Max(60, speechBounds.Width - 24);
                transformed.Height = Math.Max(48, speechBounds.Height - 24);
                transformed.FontSize = ScaleScalar(annotation.FontSize, transformMatrix);
                break;

            case AnnotationKind.Counter:
                var counterSize = annotation.Width <= 0 ? 34 : annotation.Width;
                var counterBounds = TransformBounds(
                    new Rect(annotation.StartX, annotation.StartY, counterSize, annotation.Height <= 0 ? counterSize : annotation.Height),
                    transformMatrix);
                transformed.StartX = counterBounds.Left;
                transformed.StartY = counterBounds.Top;
                transformed.Width = Math.Max(18, counterBounds.Width);
                transformed.Height = Math.Max(18, counterBounds.Height);
                transformed.FontSize = ScaleScalar(annotation.FontSize, transformMatrix);
                break;
        }

        return transformed;
    }

    private static StrokeCollection TransformStrokes(StrokeCollection strokes, Matrix transformMatrix, bool applyToStylusTip)
    {
        var transformed = CloneStrokeCollection(strokes);
        transformed.Transform(transformMatrix, applyToStylusTip);
        return transformed;
    }

    private static Rect GetBoundsRect(AnnotationRecord annotation)
    {
        var (left, top, width, height) = GetBounds(annotation);
        return new Rect(left, top, width, height);
    }

    private static Rect TransformBounds(Rect bounds, Matrix transformMatrix)
    {
        var transformedPoints = new[]
        {
            transformMatrix.Transform(bounds.TopLeft),
            transformMatrix.Transform(bounds.TopRight),
            transformMatrix.Transform(bounds.BottomLeft),
            transformMatrix.Transform(bounds.BottomRight)
        };

        var minX = transformedPoints.Min(point => point.X);
        var minY = transformedPoints.Min(point => point.Y);
        var maxX = transformedPoints.Max(point => point.X);
        var maxY = transformedPoints.Max(point => point.Y);
        return new Rect(new Point(minX, minY), new Point(maxX, maxY));
    }

    private static double ScaleScalar(double value, Matrix transformMatrix)
    {
        var scaleX = Math.Sqrt((transformMatrix.M11 * transformMatrix.M11) + (transformMatrix.M12 * transformMatrix.M12));
        var scaleY = Math.Sqrt((transformMatrix.M21 * transformMatrix.M21) + (transformMatrix.M22 * transformMatrix.M22));
        var scale = (scaleX + scaleY) / 2d;
        return Math.Max(1, value * (scale == 0 ? 1 : scale));
    }

    private static bool TryGetSelectionBounds(Point start, Point end, double minimumLength, out Rect bounds)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        bounds = new Rect(left, top, width, height);
        return width >= minimumLength && height >= minimumLength;
    }

    private double GetHighlightOpacity()
    {
        return Math.Clamp(0.18 + (ThicknessSlider.Value / 28d), 0.24, 0.72);
    }

    private int GetObfuscationBlockSize()
    {
        return Math.Clamp((int)Math.Round(6 + (ThicknessSlider.Value * 1.8)), 6, 40);
    }

    private static BitmapSource PixelateBitmapRegion(BitmapSource source, Rect selectionBounds, int blockSize)
    {
        var formatted = EnsureBgra32(source);
        var region = GetPixelRegion(formatted, selectionBounds);
        var stride = formatted.PixelWidth * 4;
        var pixels = new byte[stride * formatted.PixelHeight];
        formatted.CopyPixels(pixels, stride, 0);

        for (var blockTop = region.Y; blockTop < region.Y + region.Height; blockTop += blockSize)
        {
            var currentBlockHeight = Math.Min(blockSize, (region.Y + region.Height) - blockTop);

            for (var blockLeft = region.X; blockLeft < region.X + region.Width; blockLeft += blockSize)
            {
                var currentBlockWidth = Math.Min(blockSize, (region.X + region.Width) - blockLeft);
                long blue = 0;
                long green = 0;
                long red = 0;
                long alpha = 0;
                var pixelCount = currentBlockWidth * currentBlockHeight;

                for (var y = 0; y < currentBlockHeight; y++)
                {
                    var rowOffset = (blockTop + y) * stride;
                    for (var x = 0; x < currentBlockWidth; x++)
                    {
                        var pixelIndex = rowOffset + ((blockLeft + x) * 4);
                        blue += pixels[pixelIndex];
                        green += pixels[pixelIndex + 1];
                        red += pixels[pixelIndex + 2];
                        alpha += pixels[pixelIndex + 3];
                    }
                }

                var averageBlue = (byte)(blue / pixelCount);
                var averageGreen = (byte)(green / pixelCount);
                var averageRed = (byte)(red / pixelCount);
                var averageAlpha = (byte)(alpha / pixelCount);

                for (var y = 0; y < currentBlockHeight; y++)
                {
                    var rowOffset = (blockTop + y) * stride;
                    for (var x = 0; x < currentBlockWidth; x++)
                    {
                        var pixelIndex = rowOffset + ((blockLeft + x) * 4);
                        pixels[pixelIndex] = averageBlue;
                        pixels[pixelIndex + 1] = averageGreen;
                        pixels[pixelIndex + 2] = averageRed;
                        pixels[pixelIndex + 3] = averageAlpha;
                    }
                }
            }
        }

        var result = BitmapSource.Create(
            formatted.PixelWidth,
            formatted.PixelHeight,
            source.DpiX > 0 ? source.DpiX : 96,
            source.DpiY > 0 ? source.DpiY : 96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        result.Freeze();
        return result;
    }

    private static BitmapSource EnsureBgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32)
        {
            return source;
        }

        var converted = new FormatConvertedBitmap();
        converted.BeginInit();
        converted.Source = source;
        converted.DestinationFormat = PixelFormats.Bgra32;
        converted.EndInit();
        converted.Freeze();
        return converted;
    }

    private static Int32Rect GetPixelRegion(BitmapSource source, Rect selectionBounds)
    {
        var x = Math.Clamp((int)Math.Floor(selectionBounds.Left), 0, Math.Max(0, source.PixelWidth - 1));
        var y = Math.Clamp((int)Math.Floor(selectionBounds.Top), 0, Math.Max(0, source.PixelHeight - 1));
        var right = Math.Clamp((int)Math.Ceiling(selectionBounds.Right), x + 1, source.PixelWidth);
        var bottom = Math.Clamp((int)Math.Ceiling(selectionBounds.Bottom), y + 1, source.PixelHeight);
        return new Int32Rect(x, y, right - x, bottom - y);
    }

    private static (double Left, double Top, double Width, double Height) GetBounds(AnnotationRecord annotation)
    {
        return (
            Math.Min(annotation.StartX, annotation.EndX),
            Math.Min(annotation.StartY, annotation.EndY),
            Math.Abs(annotation.EndX - annotation.StartX),
            Math.Abs(annotation.EndY - annotation.StartY));
    }

    private static PointCollection BuildArrowHead(Point start, Point end, double size)
    {
        var vector = start - end;
        if (vector.Length == 0)
        {
            vector = new Vector(-1, 0);
        }

        vector.Normalize();
        var orthogonal = new Vector(-vector.Y, vector.X);

        var point1 = end;
        var point2 = end + (vector * size) + (orthogonal * (size * 0.45));
        var point3 = end + (vector * size) - (orthogonal * (size * 0.45));

        return [point1, point2, point3];
    }

    private static Point GetArrowShaftEnd(Point start, Point end, double inset)
    {
        var direction = end - start;
        if (direction.Length == 0)
        {
            return end;
        }

        direction.Normalize();
        return end - (direction * inset);
    }

    private static StrokeCollection CloneStrokeCollection(StrokeCollection strokes)
    {
        using var stream = new MemoryStream();
        strokes.Save(stream);
        stream.Position = 0;
        return new StrokeCollection(stream);
    }

    private bool ShouldCommitActiveTextEditOnClick(Point clickPosition)
    {
        if (_activeTextEditorHost is null)
        {
            return false;
        }

        var left = Canvas.GetLeft(_activeTextEditorHost);
        var top = Canvas.GetTop(_activeTextEditorHost);
        var width = _activeTextEditorHost.ActualWidth > 0 ? _activeTextEditorHost.ActualWidth : _activeTextEditorHost.Width;
        var height = _activeTextEditorHost.ActualHeight > 0 ? _activeTextEditorHost.ActualHeight : _activeTextEditorHost.Height;
        var editorBounds = new Rect(left, top, width, height);
        return !editorBounds.Contains(clickPosition);
    }

    private bool TryBeginTextAnnotationEdit(Point position, AnnotationKind kind)
    {
        var annotation = _annotations
            .LastOrDefault(candidate => candidate.Kind == kind && GetEditableBounds(candidate).Contains(position));

        if (annotation is null)
        {
            return false;
        }

        BeginTextAnnotationEdit(annotation, false);
        return true;
    }

    private void BeginTextAnnotationEdit(AnnotationRecord annotation, bool isNewAnnotation)
    {
        FinishTextEditing(commitChanges: true);

        _editingTextAnnotation = annotation;
        _editingOriginalText = annotation.Text ?? string.Empty;
        _isEditingNewTextAnnotation = isNewAnnotation;
        _lastValidSpeechBubbleText = annotation.Text ?? string.Empty;

        var (host, editor) = BuildTextEditor(annotation);
        _activeTextEditorHost = host;
        _activeTextEditor = editor;
        InteractionCanvas.Children.Add(host);
        PositionTextEditor(annotation, host);

        editor.Loaded += (_, _) =>
        {
            editor.Focus();
            editor.SelectAll();
        };

        SetStatus(annotation.Kind == AnnotationKind.Text
            ? "Type your text and click away to apply it."
            : "Type your speech bubble text and click away to apply it.");
    }

    private (FrameworkElement Host, TextBox Editor) BuildTextEditor(AnnotationRecord annotation)
    {
        var isSpeechBubble = annotation.Kind == AnnotationKind.SpeechBubble;
        if (!isSpeechBubble && (annotation.Width <= 0 || annotation.Height <= 0))
        {
            RefreshTextAnnotationLayout(annotation);
        }

        var bubbleWidth = annotation.Width <= 0 ? DefaultSpeechBubbleWidth : annotation.Width;
        var bubbleHeight = annotation.Height <= 0 ? DefaultSpeechBubbleHeight : annotation.Height;

        var textWidth = isSpeechBubble
            ? Math.Max(MinimumSpeechBubbleTextWidth, bubbleWidth - 28)
            : Math.Max(MinimumTextAnnotationWidth - 24, annotation.Width - 24);
        var textHeight = isSpeechBubble
            ? Math.Max(42, bubbleHeight - 20)
            : Math.Max(MinimumTextAnnotationHeight - 16, annotation.Height - 16);

        var editor = new TextBox
        {
            Text = annotation.Text ?? string.Empty,
            FontFamily = new FontFamily("Consolas"),
            FontSize = annotation.FontSize,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(annotation.ColorHex)),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            AcceptsReturn = true,
            TextWrapping = isSpeechBubble || ShouldWrapTextAnnotation(annotation.Text ?? string.Empty, annotation.FontSize)
                ? TextWrapping.Wrap
                : TextWrapping.NoWrap,
            VerticalContentAlignment = VerticalAlignment.Top,
            MinWidth = textWidth,
            Width = textWidth,
            MinHeight = textHeight,
            Height = textHeight,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            BorderBrush = Brushes.Transparent,
            CaretBrush = Brushes.White,
            FocusVisualStyle = null
        };

        if (isSpeechBubble)
        {
            editor.MaxLength = MaximumSpeechBubbleCharacters;
        }
        else
        {
            editor.MaxLength = MaximumTextAnnotationCharacters;
        }

        editor.PreviewKeyDown += ActiveTextEditor_PreviewKeyDown;
        editor.LostKeyboardFocus += ActiveTextEditor_LostKeyboardFocus;
        editor.TextChanged += ActiveTextEditor_TextChanged;

        if (isSpeechBubble)
        {
            var bubble = new Border
            {
                Width = bubbleWidth,
                Height = bubbleHeight,
                Background = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20)),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(annotation.ColorHex)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(14, 10, 14, 10),
                Child = editor
            };

            var tail = new Polygon
            {
                Fill = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20)),
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(annotation.ColorHex)),
                StrokeThickness = 2,
                Points = [new Point(28, bubbleHeight), new Point(52, bubbleHeight), new Point(18, bubbleHeight + 20)]
            };

            var bubbleHost = new Canvas
            {
                Width = bubbleWidth + 24,
                Height = bubbleHeight + 24,
                Background = Brushes.Transparent
            };

            bubbleHost.Children.Add(bubble);
            bubbleHost.Children.Add(tail);
            return (bubbleHost, editor);
        }

        var host = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(190, 18, 18, 18)),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(annotation.ColorHex)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 8, 12, 8),
            Child = editor
        };

        return (host, editor);
    }

    private void PositionTextEditor(AnnotationRecord annotation, FrameworkElement host)
    {
        Canvas.SetLeft(host, annotation.StartX);
        Canvas.SetTop(host, annotation.StartY);
    }

    private void ActiveTextEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_activeTextEditor is null)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            FinishTextEditing(commitChanges: false);
            e.Handled = true;
            return;
        }

        if (_editingTextAnnotation is not null &&
            e.Key == Key.Enter &&
            Keyboard.Modifiers == ModifierKeys.Control)
        {
            FinishTextEditing(commitChanges: true);
            e.Handled = true;
        }
    }

    private void ActiveTextEditor_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_isCommittingTextEdit)
        {
            FinishTextEditing(commitChanges: true);
        }
    }

    private void ActiveTextEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextEditorTextChanged || _activeTextEditor is null || _editingTextAnnotation is null)
        {
            return;
        }

        if (_editingTextAnnotation.Kind == AnnotationKind.SpeechBubble)
        {
            var candidateText = _activeTextEditor.Text;
            if (!TryMeasureSpeechBubbleAnnotation(candidateText, _editingTextAnnotation.FontSize, out var candidateBubbleSize))
            {
                _suppressTextEditorTextChanged = true;
                var caretIndex = Math.Max(0, Math.Min(_activeTextEditor.CaretIndex - 1, _lastValidSpeechBubbleText.Length));
                _activeTextEditor.Text = _lastValidSpeechBubbleText;
                _activeTextEditor.CaretIndex = caretIndex;
                _suppressTextEditorTextChanged = false;
                return;
            }

            _lastValidSpeechBubbleText = candidateText;
            _editingTextAnnotation.Text = candidateText;
            _editingTextAnnotation.Width = candidateBubbleSize.Width;
            _editingTextAnnotation.Height = candidateBubbleSize.Height;
            UpdateSpeechBubbleEditorLayout(_editingTextAnnotation, _activeTextEditor);
            RenderAnnotations();
            Dispatcher.BeginInvoke(() => _activeTextEditor?.UpdateLayout(), DispatcherPriority.Background);
            return;
        }

        if (_editingTextAnnotation.Kind == AnnotationKind.Text)
        {
            var candidateText = _activeTextEditor.Text;
            if (!TryMeasureTextAnnotation(candidateText, _editingTextAnnotation.FontSize, out var candidateTextSize))
            {
                _suppressTextEditorTextChanged = true;
                var caretIndex = Math.Max(0, Math.Min(_activeTextEditor.CaretIndex - 1, (_editingOriginalText ?? string.Empty).Length));
                _activeTextEditor.Text = _editingTextAnnotation.Text ?? string.Empty;
                _activeTextEditor.CaretIndex = Math.Min(caretIndex, _activeTextEditor.Text.Length);
                _suppressTextEditorTextChanged = false;
                return;
            }

            _editingTextAnnotation.Text = candidateText;
            _editingTextAnnotation.Width = candidateTextSize.Width;
            _editingTextAnnotation.Height = candidateTextSize.Height;
            UpdateTextAnnotationEditorLayout(_editingTextAnnotation, _activeTextEditor);
            RenderAnnotations();
            Dispatcher.BeginInvoke(() => _activeTextEditor?.UpdateLayout(), DispatcherPriority.Background);
            return;
        }

        _editingTextAnnotation.Text = _activeTextEditor.Text;
        RefreshTextAnnotationLayout(_editingTextAnnotation);

        RenderAnnotations();
        Dispatcher.BeginInvoke(() => _activeTextEditor?.UpdateLayout(), DispatcherPriority.Background);
    }

    private void CommitPendingTextEdit()
    {
        FinishTextEditing(commitChanges: true);
    }

    private void CancelPendingTextEdit()
    {
        FinishTextEditing(commitChanges: false);
    }

    private void FinishTextEditing(bool commitChanges)
    {
        if (_activeTextEditor is null || _editingTextAnnotation is null || _isCommittingTextEdit)
        {
            return;
        }

        _isCommittingTextEdit = true;

        var editor = _activeTextEditor;
        var annotation = _editingTextAnnotation;
        var originalText = _editingOriginalText ?? string.Empty;
        var isNewAnnotation = _isEditingNewTextAnnotation;
        var updatedText = editor.Text.Trim();

        editor.PreviewKeyDown -= ActiveTextEditor_PreviewKeyDown;
        editor.LostKeyboardFocus -= ActiveTextEditor_LostKeyboardFocus;
        editor.TextChanged -= ActiveTextEditor_TextChanged;
        InteractionCanvas.Children.Remove(_activeTextEditorHost ?? editor);

        _activeTextEditorHost = null;
        _activeTextEditor = null;
        _editingTextAnnotation = null;
        _editingOriginalText = null;
        _isEditingNewTextAnnotation = false;

        if (!commitChanges)
        {
            if (isNewAnnotation)
            {
                _annotations.Remove(annotation);
                SetStatus(annotation.Kind == AnnotationKind.Text ? "Text canceled." : "Speech bubble canceled.");
            }
            else
            {
                annotation.Text = originalText;
                RefreshTextAnnotationLayout(annotation);
                SetStatus(annotation.Kind == AnnotationKind.Text ? "Text edit canceled." : "Speech bubble edit canceled.");
            }

            RenderAnnotations();
            _isCommittingTextEdit = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(updatedText))
        {
            if (isNewAnnotation)
            {
                _annotations.Remove(annotation);
                SetStatus(annotation.Kind == AnnotationKind.Text ? "Text canceled." : "Speech bubble canceled.");
            }
            else
            {
                annotation.Text = originalText;
                RefreshTextAnnotationLayout(annotation);
                SetStatus(annotation.Kind == AnnotationKind.Text ? "Text unchanged." : "Speech bubble unchanged.");
            }

            RenderAnnotations();
            _isCommittingTextEdit = false;
            return;
        }

        annotation.Text = updatedText;
        RefreshTextAnnotationLayout(annotation);
        RenderAnnotations();

        if (isNewAnnotation || !string.Equals(originalText, updatedText, StringComparison.Ordinal))
        {
            CommitSnapshot();
            SetStatus(annotation.Kind == AnnotationKind.Text ? "Text added." : "Speech bubble added.");
        }
        else
        {
            SetStatus(annotation.Kind == AnnotationKind.Text ? "Text unchanged." : "Speech bubble unchanged.");
        }

        _isCommittingTextEdit = false;
    }

    private void RefreshTextAnnotationLayout(AnnotationRecord annotation)
    {
        if (annotation.Kind == AnnotationKind.Text)
        {
            var size = MeasureTextAnnotation(annotation);
            annotation.Width = size.Width;
            annotation.Height = size.Height;
            return;
        }

        if (annotation.Kind == AnnotationKind.SpeechBubble)
        {
            var bubbleSize = MeasureSpeechBubbleAnnotation(annotation);
            annotation.Width = bubbleSize.Width;
            annotation.Height = bubbleSize.Height;
        }
    }

    private Size MeasureTextAnnotation(AnnotationRecord annotation)
    {
        var text = string.IsNullOrWhiteSpace(annotation.Text) ? DefaultTextAnnotation : annotation.Text;
        return MeasureTextAnnotation(text, annotation.FontSize);
    }

    private Size MeasureTextAnnotation(string text, double fontSize)
    {
        TryMeasureTextAnnotation(text, fontSize, out var measured);
        return measured;
    }

    private bool TryMeasureTextAnnotation(string text, double fontSize, out Size measured)
    {
        text = string.IsNullOrWhiteSpace(text) ? DefaultTextAnnotation : text;
        var maxContentWidth = GetMaximumTextAnnotationContentWidth(fontSize);
        var longestLineWidth = GetLongestTextLineWidth(text, fontSize);
        var shouldWrap = text.Contains('\n') || text.Contains('\r') || longestLineWidth > maxContentWidth;
        var effectiveWidth = shouldWrap ? maxContentWidth : longestLineWidth;
        var contentSize = MeasureTextContent(text, fontSize, effectiveWidth);
        measured = new Size(
            Math.Max(MinimumTextAnnotationWidth, Math.Min(maxContentWidth, Math.Max(longestLineWidth, contentSize.Width)) + 24),
            Math.Max(MinimumTextAnnotationHeight, contentSize.Height + 16));
        return text.Length <= MaximumTextAnnotationCharacters;
    }

    private Size MeasureSpeechBubbleText(AnnotationRecord annotation, double bubbleWidth)
    {
        var text = string.IsNullOrWhiteSpace(annotation.Text) ? DefaultSpeechBubbleText : annotation.Text;
        var textWidth = Math.Max(MinimumSpeechBubbleTextWidth, bubbleWidth - SpeechBubbleHorizontalInsets);
        return MeasureTextBlock(text, annotation.FontSize, textWidth);
    }

    private Size MeasureSpeechBubbleAnnotation(AnnotationRecord annotation)
    {
        var text = string.IsNullOrWhiteSpace(annotation.Text) ? DefaultSpeechBubbleText : annotation.Text;
        return MeasureSpeechBubbleAnnotation(text, annotation.FontSize);
    }

    private Size MeasureSpeechBubbleAnnotation(string text, double fontSize)
    {
        TryMeasureSpeechBubbleAnnotation(text, fontSize, out var bubbleSize);
        return bubbleSize;
    }

    private bool TryMeasureSpeechBubbleAnnotation(string text, double fontSize, out Size bubbleSize)
    {
        text = string.IsNullOrWhiteSpace(text) ? DefaultSpeechBubbleText : text;
        var singleLineSize = MeasureTextBlock(text, fontSize, double.PositiveInfinity);
        var desiredBubbleWidth = Math.Min(
            MaximumSpeechBubbleWidth,
            Math.Max(DefaultSpeechBubbleWidth, singleLineSize.Width + SpeechBubbleHorizontalInsets));
        var textWidth = Math.Max(MinimumSpeechBubbleTextWidth, desiredBubbleWidth - SpeechBubbleHorizontalInsets);
        var textSize = MeasureTextBlock(text, fontSize, textWidth);
        var desiredBubbleHeight = Math.Max(DefaultSpeechBubbleHeight, textSize.Height + SpeechBubbleVerticalInsets);
        bubbleSize = new Size(desiredBubbleWidth, Math.Min(MaximumSpeechBubbleHeight, desiredBubbleHeight));
        return desiredBubbleHeight <= MaximumSpeechBubbleHeight && text.Length <= MaximumSpeechBubbleCharacters;
    }

    private void UpdateSpeechBubbleEditorLayout(AnnotationRecord annotation, TextBox editor)
    {
        if (_activeTextEditorHost is Canvas host && host.Children.Count >= 2)
        {
            host.Width = annotation.Width + 24;
            host.Height = annotation.Height + 24;

            if (host.Children[0] is Border bubble)
            {
                bubble.Width = annotation.Width;
                bubble.Height = annotation.Height;
            }

            if (host.Children[1] is Polygon tail)
            {
                tail.Points = [new Point(28, annotation.Height), new Point(52, annotation.Height), new Point(18, annotation.Height + 20)];
            }

            PositionTextEditor(annotation, host);
        }

        editor.Width = Math.Max(MinimumSpeechBubbleTextWidth, annotation.Width - SpeechBubbleHorizontalInsets);
        editor.MinWidth = editor.Width;
        editor.Height = Math.Max(42, annotation.Height - SpeechBubbleVerticalInsets);
        editor.MinHeight = editor.Height;
    }

    private void UpdateTextAnnotationEditorLayout(AnnotationRecord annotation, TextBox editor)
    {
        if (_activeTextEditorHost is Border host)
        {
            host.Width = Math.Max(MinimumTextAnnotationWidth, annotation.Width);
            host.Height = Math.Max(MinimumTextAnnotationHeight, annotation.Height);
            PositionTextEditor(annotation, host);
        }

        editor.TextWrapping = ShouldWrapTextAnnotation(annotation.Text ?? string.Empty, annotation.FontSize)
            ? TextWrapping.Wrap
            : TextWrapping.NoWrap;
        editor.Width = Math.Max(MinimumTextAnnotationWidth - 24, annotation.Width - 24);
        editor.MinWidth = editor.Width;
        editor.Height = Math.Max(MinimumTextAnnotationHeight - 16, annotation.Height - 16);
        editor.MinHeight = editor.Height;
    }

    private Size MeasureTextBlock(string text, double fontSize, double maxWidth)
    {
        var contentSize = MeasureTextContent(text, fontSize, maxWidth);
        return new Size(
            Math.Ceiling(contentSize.Width + 24),
            Math.Ceiling(contentSize.Height + 16));
    }

    private Size MeasureTextContent(string text, double fontSize, double maxWidth)
    {
        var measurementText = new TextBlock
        {
            Text = string.IsNullOrEmpty(text) ? " " : text,
            FontFamily = new FontFamily("Consolas"),
            FontSize = fontSize,
            TextWrapping = double.IsInfinity(maxWidth) ? TextWrapping.NoWrap : TextWrapping.Wrap
        };

        var measureWidth = double.IsInfinity(maxWidth) ? double.PositiveInfinity : maxWidth;
        measurementText.Measure(new Size(measureWidth, double.PositiveInfinity));

        return new Size(
            Math.Ceiling(measurementText.DesiredSize.Width),
            Math.Ceiling(measurementText.DesiredSize.Height));
    }

    private double MeasureRawTextWidth(string text, double fontSize)
    {
        return MeasureTextContent(text, fontSize, double.PositiveInfinity).Width;
    }

    private double GetMaximumTextAnnotationContentWidth(double fontSize)
    {
        return MeasureRawTextWidth(new string('W', MaximumTextAnnotationCharactersPerLine), fontSize);
    }

    private double GetLongestTextLineWidth(string text, double fontSize)
    {
        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');

        return lines
            .Select(line => MeasureRawTextWidth(string.IsNullOrEmpty(line) ? " " : line, fontSize))
            .DefaultIfEmpty(0)
            .Max();
    }

    private bool ShouldWrapTextAnnotation(string text, double fontSize)
    {
        if (text.Contains('\n') || text.Contains('\r'))
        {
            return true;
        }

        return GetLongestTextLineWidth(text, fontSize) > GetMaximumTextAnnotationContentWidth(fontSize);
    }

    private Rect GetEditableBounds(AnnotationRecord annotation)
    {
        return annotation.Kind switch
        {
            AnnotationKind.Text => new Rect(annotation.StartX, annotation.StartY, MeasureTextAnnotation(annotation).Width, MeasureTextAnnotation(annotation).Height),
            AnnotationKind.SpeechBubble => new Rect(
                annotation.StartX,
                annotation.StartY,
                (annotation.Width <= 0 ? DefaultSpeechBubbleWidth : annotation.Width) + 24,
                (annotation.Height <= 0 ? DefaultSpeechBubbleHeight : annotation.Height) + 24),
            _ => Rect.Empty
        };
    }

    private bool IsEditorSurfaceReady()
    {
        return ThicknessSlider is not null &&
               InkLayer is not null &&
               AnnotationCanvas is not null &&
               InteractionCanvas is not null &&
               BaseImage is not null;
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    private enum EditorTool
    {
        Pen,
        Highlighter,
        Rectangle,
        Ellipse,
        Line,
        Arrow,
        Text,
        SpeechBubble,
        Counter,
        Obfuscate,
        Eraser
    }
}
