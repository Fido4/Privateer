namespace Privateer.Desktop.Models;

public enum AnnotationKind
{
    Highlight,
    Rectangle,
    Ellipse,
    Line,
    Arrow,
    Text,
    SpeechBubble,
    Counter
}

public sealed class AnnotationRecord
{
    public AnnotationKind Kind { get; set; }

    public double StartX { get; set; }

    public double StartY { get; set; }

    public double EndX { get; set; }

    public double EndY { get; set; }

    public string ColorHex { get; set; } = "#FF3B82F6";

    public double Thickness { get; set; } = 4;

    public double Opacity { get; set; } = 1;

    public double FontSize { get; set; } = 20;

    public string? Text { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public AnnotationRecord Clone()
    {
        return new AnnotationRecord
        {
            Kind = Kind,
            StartX = StartX,
            StartY = StartY,
            EndX = EndX,
            EndY = EndY,
            ColorHex = ColorHex,
            Thickness = Thickness,
            Opacity = Opacity,
            FontSize = FontSize,
            Text = Text,
            Width = Width,
            Height = Height
        };
    }
}
