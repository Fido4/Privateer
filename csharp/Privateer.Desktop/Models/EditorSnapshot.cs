using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Ink;
using System.Windows.Media.Imaging;

namespace Privateer.Desktop.Models;

public sealed class EditorSnapshot
{
    public EditorSnapshot(BitmapSource baseImage, StrokeCollection strokes, IEnumerable<AnnotationRecord> annotations, int nextCounter)
    {
        BaseImage = baseImage;
        Strokes = CloneStrokes(strokes);
        Annotations = annotations.Select(annotation => annotation.Clone()).ToList();
        NextCounter = nextCounter;
    }

    public BitmapSource BaseImage { get; }

    public StrokeCollection Strokes { get; }

    public IReadOnlyList<AnnotationRecord> Annotations { get; }

    public int NextCounter { get; }

    private static StrokeCollection CloneStrokes(StrokeCollection strokes)
    {
        using var stream = new MemoryStream();
        strokes.Save(stream);
        stream.Position = 0;
        return new StrokeCollection(stream);
    }
}
