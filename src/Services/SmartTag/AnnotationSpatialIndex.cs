using System;
using System.Collections.Generic;
using AJTools.Utils;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Lightweight 2D spatial index for annotation boxes in view-plane coordinates.
    /// </summary>
    internal sealed class AnnotationSpatialIndex
    {
        private const int MaxCellsPerBox = 4096;

        private readonly double _cellSize;
        private readonly Dictionary<long, List<AnnotationBox>> _cells;
        private readonly List<AnnotationBox> _oversizedBoxes;
        private readonly HashSet<AnnotationBox> _registered;

        private struct CellRange
        {
            public int MinX;
            public int MaxX;
            public int MinY;
            public int MaxY;
        }

        public AnnotationSpatialIndex(double cellSize)
        {
            double minCellSize = 1.0 * Constants.MM_TO_FEET;
            _cellSize = cellSize > minCellSize ? cellSize : minCellSize;
            _cells = new Dictionary<long, List<AnnotationBox>>();
            _oversizedBoxes = new List<AnnotationBox>();
            _registered = new HashSet<AnnotationBox>();
        }

        public void AddRange(IEnumerable<AnnotationBox> boxes)
        {
            if (boxes == null)
                return;

            foreach (AnnotationBox box in boxes)
                Add(box);
        }

        public void Add(AnnotationBox box)
        {
            if (box == null)
                return;

            if (!_registered.Add(box))
                return;

            if (!TryGetCellRange(box, out CellRange range))
            {
                _oversizedBoxes.Add(box);
                return;
            }

            long cellCount = (long)(range.MaxX - range.MinX + 1) * (range.MaxY - range.MinY + 1);
            if (cellCount <= 0 || cellCount > MaxCellsPerBox)
            {
                _oversizedBoxes.Add(box);
                return;
            }

            for (int x = range.MinX; x <= range.MaxX; x++)
            {
                for (int y = range.MinY; y <= range.MaxY; y++)
                {
                    long key = ComposeCellKey(x, y);
                    if (!_cells.TryGetValue(key, out List<AnnotationBox> bucket))
                    {
                        bucket = new List<AnnotationBox>();
                        _cells[key] = bucket;
                    }

                    bucket.Add(box);
                }
            }
        }

        public List<AnnotationBox> Query(AnnotationBox region)
        {
            var result = new List<AnnotationBox>();
            if (region == null)
                return result;

            if (!TryGetCellRange(region, out CellRange range))
                return CollectOversizedIntersecting(region, result, null);

            var seen = new HashSet<AnnotationBox>();

            for (int x = range.MinX; x <= range.MaxX; x++)
            {
                for (int y = range.MinY; y <= range.MaxY; y++)
                {
                    long key = ComposeCellKey(x, y);
                    if (!_cells.TryGetValue(key, out List<AnnotationBox> bucket) || bucket == null)
                        continue;

                    foreach (AnnotationBox box in bucket)
                    {
                        if (box == null || !seen.Add(box))
                            continue;

                        if (box.Overlaps(region))
                            result.Add(box);
                    }
                }
            }

            return CollectOversizedIntersecting(region, result, seen);
        }

        private List<AnnotationBox> CollectOversizedIntersecting(
            AnnotationBox region,
            List<AnnotationBox> result,
            HashSet<AnnotationBox> seen)
        {
            foreach (AnnotationBox box in _oversizedBoxes)
            {
                if (box == null)
                    continue;

                if (seen != null && !seen.Add(box))
                    continue;

                if (box.Overlaps(region))
                    result.Add(box);
            }

            return result;
        }

        private bool TryGetCellRange(AnnotationBox box, out CellRange range)
        {
            range = new CellRange();
            if (box == null)
                return false;

            if (!TryGetCellCoordinate(box.MinX, out int minX)
                || !TryGetCellCoordinate(box.MaxX, out int maxX)
                || !TryGetCellCoordinate(box.MinY, out int minY)
                || !TryGetCellCoordinate(box.MaxY, out int maxY))
            {
                return false;
            }

            if (minX > maxX)
            {
                int temp = minX;
                minX = maxX;
                maxX = temp;
            }

            if (minY > maxY)
            {
                int temp = minY;
                minY = maxY;
                maxY = temp;
            }

            range.MinX = minX;
            range.MaxX = maxX;
            range.MinY = minY;
            range.MaxY = maxY;
            return true;
        }

        private bool TryGetCellCoordinate(double value, out int coordinate)
        {
            coordinate = 0;
            if (double.IsNaN(value) || double.IsInfinity(value))
                return false;

            double normalized = value / _cellSize;
            if (double.IsNaN(normalized) || double.IsInfinity(normalized))
                return false;

            double floored = Math.Floor(normalized);
            if (floored < int.MinValue || floored > int.MaxValue)
                return false;

            coordinate = (int)floored;
            return true;
        }

        private static long ComposeCellKey(int x, int y)
        {
            return unchecked(((long)x << 32) | (uint)y);
        }
    }
}
