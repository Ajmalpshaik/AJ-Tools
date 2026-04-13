using System;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Lightweight axis-aligned 2D rectangle for annotation clash detection in view plane.
    /// </summary>
    internal sealed class AnnotationBox
    {
        public double MinX { get; private set; }
        public double MinY { get; private set; }
        public double MaxX { get; private set; }
        public double MaxY { get; private set; }

        public AnnotationBox(double minX, double minY, double maxX, double maxY)
        {
            MinX = Math.Min(minX, maxX);
            MinY = Math.Min(minY, maxY);
            MaxX = Math.Max(minX, maxX);
            MaxY = Math.Max(minY, maxY);
        }

        public AnnotationBox Inflated(double margin)
        {
            return new AnnotationBox(
                MinX - margin, MinY - margin,
                MaxX + margin, MaxY + margin);
        }

        public bool Overlaps(AnnotationBox other)
        {
            if (other == null)
                return false;

            if (MaxX <= other.MinX || MinX >= other.MaxX)
                return false;
            if (MaxY <= other.MinY || MinY >= other.MaxY)
                return false;
            return true;
        }

        public double DistanceTo(AnnotationBox other)
        {
            if (other == null)
                return double.MaxValue;

            double dx = 0;
            if (MaxX < other.MinX)
                dx = other.MinX - MaxX;
            else if (MinX > other.MaxX)
                dx = MinX - other.MaxX;

            double dy = 0;
            if (MaxY < other.MinY)
                dy = other.MinY - MaxY;
            else if (MinY > other.MaxY)
                dy = MinY - other.MaxY;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        public double OverlapArea(AnnotationBox other)
        {
            if (other == null)
                return 0;

            double overlapX = Math.Max(0, Math.Min(MaxX, other.MaxX) - Math.Max(MinX, other.MinX));
            double overlapY = Math.Max(0, Math.Min(MaxY, other.MaxY) - Math.Max(MinY, other.MinY));
            return overlapX * overlapY;
        }
    }
}
