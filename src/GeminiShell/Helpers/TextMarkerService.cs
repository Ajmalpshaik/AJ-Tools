using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace AJTools.GeminiShell.Helpers
{
    public interface ITextMarkerService
    {
        ITextMarker Create(int startOffset, int length);
        IEnumerable<ITextMarker> GetMarkersAtOffset(int offset);
        IEnumerable<ITextMarker> TextMarkers { get; }
        void RemoveAll(Predicate<ITextMarker> predicate);
        void Remove(ITextMarker marker);
        void Clear();
    }

    public interface ITextMarker
    {
        int StartOffset { get; }
        int EndOffset { get; }
        int Length { get; }
        void Delete();
        bool IsDeleted { get; }
        Color? BackgroundColor { get; set; }
        Color? ForegroundColor { get; set; }
        FontWeight? FontWeight { get; set; }
        FontStyle? FontStyle { get; set; }
        TextMarkerTypes MarkerTypes { get; set; }
        Color MarkerColor { get; set; }
        object Tag { get; set; }
        string ToolTip { get; set; }
    }

    [Flags]
    public enum TextMarkerTypes
    {
        None = 0x0000,
        SquigglyUnderline = 0x001,
        NormalUnderline = 0x002,
        DottedUnderline = 0x004
    }

    public class TextMarkerService : DocumentColorizingTransformer, IBackgroundRenderer, ITextMarkerService, ITextViewConnect
    {
        private TextSegmentCollection<TextMarker> markers;
        private TextDocument document;

        public TextMarkerService(TextDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            this.document = document;
            this.markers = new TextSegmentCollection<TextMarker>(document);
        }

        public ITextMarker Create(int startOffset, int length)
        {
            if (markers == null)
                throw new InvalidOperationException("Cannot create a marker when not attached to a document");

            int textLength = document.TextLength;
            if (startOffset < 0 || startOffset > textLength)
                throw new ArgumentOutOfRangeException(nameof(startOffset), startOffset, "Value must be between 0 and " + textLength);
            if (length < 0 || startOffset + length > textLength)
                throw new ArgumentOutOfRangeException(nameof(length), length, "Length must not cause the marker to fall outside the document");

            TextMarker m = new TextMarker(this, startOffset, length);
            markers.Add(m);
            return m;
        }

        public IEnumerable<ITextMarker> GetMarkersAtOffset(int offset)
        {
            if (markers == null) return Enumerable.Empty<ITextMarker>();
            return markers.FindSegmentsContaining(offset);
        }

        public IEnumerable<ITextMarker> TextMarkers
        {
            get { return markers ?? Enumerable.Empty<ITextMarker>(); }
        }

        public void RemoveAll(Predicate<ITextMarker> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (markers != null)
            {
                foreach (TextMarker m in markers.ToArray())
                {
                    if (predicate(m)) Remove(m);
                }
            }
        }

        public void Remove(ITextMarker marker)
        {
            if (marker == null) throw new ArgumentNullException(nameof(marker));
            TextMarker m = marker as TextMarker;
            if (markers != null && markers.Remove(m))
            {
                Redraw(m);
                m.OnDeleted();
            }
        }

        public void Clear()
        {
            if (markers != null)
            {
                foreach (TextMarker m in markers.ToArray())
                {
                    Remove(m);
                }
            }
        }

        internal void RemoveInternal(TextMarker marker)
        {
            if (markers.Remove(marker))
            {
                Redraw(marker);
                marker.OnDeleted();
            }
        }

        private void Redraw(ISegment segment)
        {
            foreach (var view in textViews)
            {
                view.Redraw(segment, System.Windows.Threading.DispatcherPriority.Normal);
            }
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (markers == null) return;
            int lineStart = line.Offset;
            int lineEnd = lineStart + line.Length;
            foreach (TextMarker marker in markers.FindOverlappingSegments(lineStart, line.Length))
            {
                Brush foregroundBrush = null;
                if (marker.ForegroundColor != null)
                {
                    foregroundBrush = new SolidColorBrush(marker.ForegroundColor.Value);
                    foregroundBrush.Freeze();
                }
                ChangeLinePart(
                    Math.Max(marker.StartOffset, lineStart),
                    Math.Min(marker.EndOffset, lineEnd),
                    element =>
                    {
                        if (foregroundBrush != null) element.TextRunProperties.SetForegroundBrush(foregroundBrush);
                    }
                );
            }
        }

        public KnownLayer Layer { get { return KnownLayer.Selection; } }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView == null) throw new ArgumentNullException(nameof(textView));
            if (drawingContext == null) throw new ArgumentNullException(nameof(drawingContext));
            if (markers == null || !textView.VisualLinesValid) return;

            var visualLines = textView.VisualLines;
            if (visualLines.Count == 0) return;

            int viewStart = visualLines.First().FirstDocumentLine.Offset;
            int viewEnd = visualLines.Last().LastDocumentLine.EndOffset;

            foreach (TextMarker marker in markers.FindOverlappingSegments(viewStart, viewEnd - viewStart))
            {
                if (marker.BackgroundColor != null)
                {
                    BackgroundGeometryBuilder geoBuilder = new BackgroundGeometryBuilder();
                    geoBuilder.AlignToWholePixels = true;
                    geoBuilder.CornerRadius = 3;
                    geoBuilder.AddSegment(textView, marker);
                    Geometry geometry = geoBuilder.CreateGeometry();
                    if (geometry != null)
                    {
                        Color color = marker.BackgroundColor.Value;
                        SolidColorBrush brush = new SolidColorBrush(color);
                        brush.Freeze();
                        drawingContext.DrawGeometry(brush, null, geometry);
                    }
                }
                
                var underlineMarkerTypes = TextMarkerTypes.SquigglyUnderline | TextMarkerTypes.NormalUnderline | TextMarkerTypes.DottedUnderline;
                if ((marker.MarkerTypes & underlineMarkerTypes) != 0)
                {
                    foreach (Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker))
                    {
                        Point startPoint = r.BottomLeft;
                        Point endPoint = r.BottomRight;

                        Brush markerBrush = new SolidColorBrush(marker.MarkerColor);
                        markerBrush.Freeze();

                        if ((marker.MarkerTypes & TextMarkerTypes.SquigglyUnderline) != 0)
                        {
                            double offset = 2.5;
                            int count = Math.Max((int)((endPoint.X - startPoint.X) / offset) + 1, 4);

                            StreamGeometry geometry = new StreamGeometry();
                            using (StreamGeometryContext ctx = geometry.Open())
                            {
                                ctx.BeginFigure(startPoint, false, false);
                                ctx.PolyLineTo(CreatePoints(startPoint, endPoint, offset, count).ToArray(), true, false);
                            }
                            geometry.Freeze();
                            Pen usedPen = new Pen(markerBrush, 1);
                            usedPen.Freeze();
                            drawingContext.DrawGeometry(Brushes.Transparent, usedPen, geometry);
                        }
                        if ((marker.MarkerTypes & TextMarkerTypes.NormalUnderline) != 0)
                        {
                            Pen usedPen = new Pen(markerBrush, 1);
                            usedPen.Freeze();
                            drawingContext.DrawLine(usedPen, startPoint, endPoint);
                        }
                        if ((marker.MarkerTypes & TextMarkerTypes.DottedUnderline) != 0)
                        {
                            Pen usedPen = new Pen(markerBrush, 1);
                            usedPen.DashStyle = DashStyles.Dot;
                            usedPen.Freeze();
                            drawingContext.DrawLine(usedPen, startPoint, endPoint);
                        }
                    }
                }
            }
        }

        private IEnumerable<Point> CreatePoints(Point start, Point end, double offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new Point(start.X + i * offset, start.Y - ((i + 1) % 2 == 0 ? offset : 0));
            }
        }

        private readonly List<TextView> textViews = new List<TextView>();

        void ITextViewConnect.AddToTextView(TextView textView)
        {
            if (textView != null && !textViews.Contains(textView))
            {
                textViews.Add(textView);
            }
        }

        void ITextViewConnect.RemoveFromTextView(TextView textView)
        {
            if (textView != null)
            {
                textViews.Remove(textView);
            }
        }
    }

    public sealed class TextMarker : TextSegment, ITextMarker
    {
        private readonly TextMarkerService service;

        public TextMarker(TextMarkerService service, int startOffset, int length)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            this.service = service;
            this.StartOffset = startOffset;
            this.Length = length;
            this.MarkerTypes = TextMarkerTypes.None;
        }

        public Color? BackgroundColor { get; set; }
        public Color? ForegroundColor { get; set; }
        public FontWeight? FontWeight { get; set; }
        public FontStyle? FontStyle { get; set; }
        public TextMarkerTypes MarkerTypes { get; set; }
        public Color MarkerColor { get; set; }
        public object Tag { get; set; }
        public string ToolTip { get; set; }

        public bool IsDeleted { get; private set; }

        public void Delete()
        {
            service.RemoveInternal(this);
        }

        internal void OnDeleted()
        {
            IsDeleted = true;
        }
    }
}
