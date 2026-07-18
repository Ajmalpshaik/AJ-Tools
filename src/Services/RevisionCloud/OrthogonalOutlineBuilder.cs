using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Utils;

namespace AJTools.Services.RevisionCloud
{
    internal static class OrthogonalOutlineBuilder
    {
        private const int MaxGridDimension = 1200;

        public static List<List<UV>> BuildOutlines(
            IEnumerable<UVRect> sourceRects,
            double offsetFeet,
            double preferredCellSizeFeet)
        {
            var rects = new List<UVRect>();
            if (sourceRects != null)
            {
                foreach (var rect in sourceRects)
                {
                    if (rect == null) continue;
                    rects.Add(new UVRect(
                        rect.MinU - offsetFeet,
                        rect.MaxU + offsetFeet,
                        rect.MinV - offsetFeet,
                        rect.MaxV + offsetFeet));
                }
            }

            if (rects.Count == 0)
                return new List<List<UV>>();

            var grid = BuildGrid(rects, preferredCellSizeFeet);
            if (grid == null)
                return new List<List<UV>>();

            var components = ExtractComponents(grid);
            var outlines = new List<List<UV>>();

            foreach (var component in components)
            {
                var loops = TraceComponentLoops(grid, component);
                if (loops.Count == 0)
                    continue;

                var outer = PickLargestLoop(loops);
                if (outer == null || outer.Count < 4)
                    continue;

                var simplified = SimplifyOrthogonal(outer);
                if (simplified.Count < 4)
                    continue;

                EnsureClockwise(simplified);
                outlines.Add(simplified);
            }

            return outlines;
        }

        private static OutlineGrid BuildGrid(List<UVRect> rects, double preferredCellSizeFeet)
        {
            if (rects == null || rects.Count == 0)
                return null;

            double minU = double.MaxValue;
            double maxU = double.MinValue;
            double minV = double.MaxValue;
            double maxV = double.MinValue;

            foreach (var r in rects)
            {
                if (r.MinU < minU) minU = r.MinU;
                if (r.MaxU > maxU) maxU = r.MaxU;
                if (r.MinV < minV) minV = r.MinV;
                if (r.MaxV > maxV) maxV = r.MaxV;
            }

            if (minU > maxU || minV > maxV)
                return null;

            double cell = preferredCellSizeFeet > 1e-9 ? preferredCellSizeFeet : 10.0 * Constants.MM_TO_FEET;
            int cols = (int)Math.Ceiling((maxU - minU) / cell) + 1;
            int rows = (int)Math.Ceiling((maxV - minV) / cell) + 1;

            int maxDim = Math.Max(cols, rows);
            if (maxDim > MaxGridDimension)
            {
                double scale = (double)maxDim / MaxGridDimension;
                cell *= scale;
                cols = (int)Math.Ceiling((maxU - minU) / cell) + 1;
                rows = (int)Math.Ceiling((maxV - minV) / cell) + 1;
            }

            if (cols < 1 || rows < 1)
                return null;

            var grid = new bool[cols, rows];
            foreach (var r in rects)
            {
                int c0 = Math.Max(0, (int)Math.Floor((r.MinU - minU) / cell));
                int c1 = Math.Min(cols - 1, (int)Math.Floor((r.MaxU - minU) / cell));
                int r0 = Math.Max(0, (int)Math.Floor((r.MinV - minV) / cell));
                int r1 = Math.Min(rows - 1, (int)Math.Floor((r.MaxV - minV) / cell));

                for (int c = c0; c <= c1; c++)
                {
                    for (int rr = r0; rr <= r1; rr++)
                        grid[c, rr] = true;
                }
            }

            return new OutlineGrid(grid, cols, rows, minU, minV, cell);
        }

        private static List<HashSet<long>> ExtractComponents(OutlineGrid grid)
        {
            var components = new List<HashSet<long>>();
            var visited = new bool[grid.Cols, grid.Rows];
            int[] dc = { 1, -1, 0, 0 };
            int[] dr = { 0, 0, 1, -1 };

            for (int c = 0; c < grid.Cols; c++)
            {
                for (int r = 0; r < grid.Rows; r++)
                {
                    if (!grid.Cells[c, r] || visited[c, r])
                        continue;

                    var comp = new HashSet<long>();
                    var q = new Queue<(int c, int r)>();
                    q.Enqueue((c, r));
                    visited[c, r] = true;

                    while (q.Count > 0)
                    {
                        var cur = q.Dequeue();
                        comp.Add(CellKey(cur.c, cur.r, grid.Cols));

                        for (int i = 0; i < 4; i++)
                        {
                            int nc = cur.c + dc[i];
                            int nr = cur.r + dr[i];
                            if (nc < 0 || nc >= grid.Cols || nr < 0 || nr >= grid.Rows)
                                continue;
                            if (!grid.Cells[nc, nr] || visited[nc, nr])
                                continue;
                            visited[nc, nr] = true;
                            q.Enqueue((nc, nr));
                        }
                    }

                    if (comp.Count > 0)
                        components.Add(comp);
                }
            }

            return components;
        }

        private static List<List<UV>> TraceComponentLoops(OutlineGrid grid, HashSet<long> component)
        {
            var edgeMap = new Dictionary<long, List<long>>();

            foreach (var cellKey in component)
            {
                DecodeCellKey(cellKey, grid.Cols, out int c, out int r);

                if (!component.Contains(CellKey(c, r - 1, grid.Cols)))
                    AddEdge(edgeMap, CornerKey(c, r, grid.Cols), CornerKey(c + 1, r, grid.Cols));
                if (!component.Contains(CellKey(c + 1, r, grid.Cols)))
                    AddEdge(edgeMap, CornerKey(c + 1, r, grid.Cols), CornerKey(c + 1, r + 1, grid.Cols));
                if (!component.Contains(CellKey(c, r + 1, grid.Cols)))
                    AddEdge(edgeMap, CornerKey(c + 1, r + 1, grid.Cols), CornerKey(c, r + 1, grid.Cols));
                if (!component.Contains(CellKey(c - 1, r, grid.Cols)))
                    AddEdge(edgeMap, CornerKey(c, r + 1, grid.Cols), CornerKey(c, r, grid.Cols));
            }

            var loops = new List<List<UV>>();
            int safety = edgeMap.Count * 8 + 1000;

            while (edgeMap.Count > 0 && safety-- > 0)
            {
                long start = GetMinKey(edgeMap);
                long current = start;
                var loopKeys = new List<long>();
                int loopSafety = edgeMap.Count * 4 + 1000;

                while (loopSafety-- > 0)
                {
                    loopKeys.Add(current);
                    if (!edgeMap.TryGetValue(current, out var nexts) || nexts.Count == 0)
                        break;

                    long next = nexts[nexts.Count - 1];
                    nexts.RemoveAt(nexts.Count - 1);
                    if (nexts.Count == 0)
                        edgeMap.Remove(current);

                    current = next;
                    if (current == start)
                        break;
                }

                if (loopKeys.Count < 4 || current != start)
                    continue;

                var loop = new List<UV>(loopKeys.Count);
                foreach (var key in loopKeys)
                {
                    DecodeCornerKey(key, grid.Cols, out int cc, out int cr);
                    loop.Add(grid.CornerToUV(cc, cr));
                }

                if (loop.Count >= 4)
                    loops.Add(loop);
            }

            return loops;
        }

        private static List<UV> PickLargestLoop(List<List<UV>> loops)
        {
            List<UV> best = null;
            double bestArea = double.MinValue;

            foreach (var loop in loops)
            {
                double area = Math.Abs(GetSignedArea(loop));
                if (area > bestArea)
                {
                    bestArea = area;
                    best = loop;
                }
            }

            return best;
        }

        private static List<UV> SimplifyOrthogonal(List<UV> loop)
        {
            if (loop == null || loop.Count < 4)
                return loop ?? new List<UV>();

            var result = new List<UV>();
            int n = loop.Count;
            for (int i = 0; i < n; i++)
            {
                UV prev = loop[(i - 1 + n) % n];
                UV curr = loop[i];
                UV next = loop[(i + 1) % n];

                bool collinearU = Math.Abs(prev.U - curr.U) < 1e-12 && Math.Abs(curr.U - next.U) < 1e-12;
                bool collinearV = Math.Abs(prev.V - curr.V) < 1e-12 && Math.Abs(curr.V - next.V) < 1e-12;
                if (!collinearU && !collinearV)
                    result.Add(curr);
            }

            return result.Count >= 4 ? result : loop;
        }

        private static void EnsureClockwise(List<UV> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return;

            if (GetSignedArea(polygon) > 0)
                polygon.Reverse();
        }

        private static double GetSignedArea(List<UV> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return 0;

            double area = 0;
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                UV a = polygon[i];
                UV b = polygon[(i + 1) % n];
                area += (a.U * b.V) - (b.U * a.V);
            }

            return area * 0.5;
        }

        private static void AddEdge(Dictionary<long, List<long>> edgeMap, long from, long to)
        {
            if (!edgeMap.TryGetValue(from, out var targets))
            {
                targets = new List<long>();
                edgeMap[from] = targets;
            }
            targets.Add(to);
        }

        private static long GetMinKey(Dictionary<long, List<long>> edgeMap)
        {
            long min = long.MaxValue;
            foreach (var key in edgeMap.Keys)
            {
                if (key < min) min = key;
            }
            return min;
        }

        private static long CornerKey(int col, int row, int cols)
        {
            return (long)row * (cols + 1) + col;
        }

        private static void DecodeCornerKey(long key, int cols, out int col, out int row)
        {
            int w = cols + 1;
            row = (int)(key / w);
            col = (int)(key % w);
        }

        private static long CellKey(int col, int row, int cols)
        {
            return ((long)row << 32) | (uint)col;
        }

        private static void DecodeCellKey(long key, int cols, out int col, out int row)
        {
            row = (int)(key >> 32);
            col = (int)(key & 0xffffffff);
        }

        private sealed class OutlineGrid
        {
            public OutlineGrid(bool[,] cells, int cols, int rows, double minU, double minV, double cellSize)
            {
                Cells = cells;
                Cols = cols;
                Rows = rows;
                MinU = minU;
                MinV = minV;
                CellSize = cellSize;
            }

            public bool[,] Cells { get; }
            public int Cols { get; }
            public int Rows { get; }
            public double MinU { get; }
            public double MinV { get; }
            public double CellSize { get; }

            public UV CornerToUV(int col, int row)
            {
                return new UV(MinU + col * CellSize, MinV + row * CellSize);
            }
        }
    }
}
