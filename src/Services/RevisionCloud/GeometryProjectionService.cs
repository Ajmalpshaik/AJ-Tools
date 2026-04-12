using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AJTools.Services.RevisionCloud
{
    internal static class GeometryProjectionService
    {
        public static UVRect GetProjectedBoundingBox(Element element, View view)
        {
            return GetProjectedBoundingBox(element, view, 0.0);
        }

        public static UVRect GetProjectedBoundingBox(Element element, View view, double axisAngle)
        {
            if (element == null || view == null)
                return null;

            var viewPlane = GetViewPlane(view);
            if (viewPlane == null)
                return null;

            var projectedPoints = new List<UV>();

            var options = new Options { View = view, ComputeReferences = false };
            GeometryElement geomElement = element.get_Geometry(options);
            if (geomElement != null)
                CollectGeometryPoints(geomElement, viewPlane, projectedPoints);

            // Fallback: use element bounding box if visible geometry is unavailable.
            if (projectedPoints.Count < 2)
            {
                BoundingBoxXYZ bbox = element.get_BoundingBox(view);
                if (bbox == null)
                    return null;

                AddBoundingBoxCorners(bbox, viewPlane, projectedPoints);
            }

            if (projectedPoints.Count < 2)
                return null;

            double cosA = Math.Cos(-axisAngle);
            double sinA = Math.Sin(-axisAngle);
            bool rotateToAxisFrame = Math.Abs(axisAngle) > 1e-9;

            double minU = double.MaxValue;
            double maxU = double.MinValue;
            double minV = double.MaxValue;
            double maxV = double.MinValue;

            foreach (var pt in projectedPoints)
            {
                double u = pt.U;
                double v = pt.V;
                if (rotateToAxisFrame)
                {
                    double ru = (pt.U * cosA) - (pt.V * sinA);
                    double rv = (pt.U * sinA) + (pt.V * cosA);
                    u = ru;
                    v = rv;
                }

                if (u < minU) minU = u;
                if (u > maxU) maxU = u;
                if (v < minV) minV = v;
                if (v > maxV) maxV = v;
            }

            if (minU > maxU || minV > maxV)
                return null;

            // Ensure non-zero extents so cloud curves are always valid.
            if (maxU - minU < 1e-9)
            {
                minU -= 0.01;
                maxU += 0.01;
            }

            if (maxV - minV < 1e-9)
            {
                minV -= 0.01;
                maxV += 0.01;
            }

            return new UVRect(minU, maxU, minV, maxV);
        }

        private static void AddBoundingBoxCorners(BoundingBoxXYZ bbox, ViewPlaneData plane, List<UV> points)
        {
            double[] xs = { bbox.Min.X, bbox.Max.X };
            double[] ys = { bbox.Min.Y, bbox.Max.Y };
            double[] zs = { bbox.Min.Z, bbox.Max.Z };

            foreach (double x in xs)
                foreach (double y in ys)
                    foreach (double z in zs)
                        points.Add(ProjectToViewPlane(new XYZ(x, y, z), plane));
        }

        public static ViewPlaneData GetViewPlane(View view)
        {
            if (view == null)
                return null;

            XYZ origin = view.Origin;
            XYZ right = view.RightDirection;
            XYZ up = view.UpDirection;
            XYZ normal = view.ViewDirection;

            if (origin == null || right == null || up == null || normal == null)
                return null;

            return new ViewPlaneData(origin, right, up, normal);
        }

        public static UV ProjectToViewPlane(XYZ point, ViewPlaneData plane)
        {
            XYZ delta = point - plane.Origin;
            return new UV(delta.DotProduct(plane.Right), delta.DotProduct(plane.Up));
        }

        public static XYZ UnprojectFromViewPlane(UV uv, ViewPlaneData plane)
        {
            return plane.Origin + uv.U * plane.Right + uv.V * plane.Up;
        }

        /// <summary>
        /// Tries to get a projected axis angle (radians in view UV plane) for an element.
        /// Angle is directional but should be treated as axis-aligned (a and a+PI equivalent).
        /// </summary>
        public static bool TryGetElementProjectedAxisAngle(Element element, ViewPlaneData plane, out double angle)
        {
            angle = 0.0;
            if (element == null || plane == null)
                return false;

            // Prefer LocationCurve (duct/pipe/tray/conduit/wall, etc.).
            if (element.Location is LocationCurve locCurve)
            {
                Curve curve = locCurve.Curve;
                if (curve != null && curve.IsBound)
                {
                    XYZ dir3D = curve.GetEndPoint(1) - curve.GetEndPoint(0);
                    if (dir3D.GetLength() > 1e-9)
                    {
                        double u = dir3D.DotProduct(plane.Right);
                        double v = dir3D.DotProduct(plane.Up);
                        if (Math.Abs(u) > 1e-12 || Math.Abs(v) > 1e-12)
                        {
                            angle = Math.Atan2(v, u);
                            return true;
                        }
                    }
                }
            }

            // Fallback: LocationPoint rotation (some families throw on Rotation, so guard it).
            if (element.Location is LocationPoint locPoint)
            {
                try
                {
                    double rotation = locPoint.Rotation;
                    if (Math.Abs(rotation) > 1e-9)
                    {
                        XYZ rotatedRight = new XYZ(Math.Cos(rotation), Math.Sin(rotation), 0);
                        double u = rotatedRight.DotProduct(plane.Right);
                        double v = rotatedRight.DotProduct(plane.Up);
                        if (Math.Abs(u) > 1e-12 || Math.Abs(v) > 1e-12)
                        {
                            angle = Math.Atan2(v, u);
                            return true;
                        }
                    }
                }
                catch
                {
                    // Ignore unsupported Rotation.
                }
            }

            return false;
        }

        private static void CollectGeometryPoints(GeometryElement geomElement, ViewPlaneData plane, List<UV> points)
        {
            foreach (GeometryObject geomObj in geomElement)
            {
                if (geomObj is Solid solid)
                {
                    foreach (Edge edge in solid.Edges)
                    {
                        Curve edgeCurve = edge.AsCurve();
                        if (edgeCurve == null) continue;
                        AddCurveEndpoints(edgeCurve, plane, points);
                    }
                }
                else if (geomObj is Curve curve)
                {
                    AddCurveEndpoints(curve, plane, points);
                }
                else if (geomObj is Mesh mesh)
                {
                    for (int i = 0; i < mesh.NumTriangles; i++)
                    {
                        MeshTriangle tri = mesh.get_Triangle(i);
                        for (int j = 0; j < 3; j++)
                            points.Add(ProjectToViewPlane(tri.get_Vertex(j), plane));
                    }
                }
                else if (geomObj is GeometryInstance instance)
                {
                    GeometryElement instanceGeom = instance.GetInstanceGeometry();
                    if (instanceGeom != null)
                        CollectGeometryPoints(instanceGeom, plane, points);
                }
            }
        }

        private static void AddCurveEndpoints(Curve curve, ViewPlaneData plane, List<UV> points)
        {
            if (curve == null || !curve.IsBound)
                return;

            points.Add(ProjectToViewPlane(curve.GetEndPoint(0), plane));
            points.Add(ProjectToViewPlane(curve.GetEndPoint(1), plane));
        }
    }

    internal class UVRect
    {
        public double MinU { get; }
        public double MaxU { get; }
        public double MinV { get; }
        public double MaxV { get; }

        public UVRect(double minU, double maxU, double minV, double maxV)
        {
            MinU = minU;
            MaxU = maxU;
            MinV = minV;
            MaxV = maxV;
        }
    }

    internal class ViewPlaneData
    {
        public XYZ Origin { get; }
        public XYZ Right { get; }
        public XYZ Up { get; }
        public XYZ Normal { get; }

        public ViewPlaneData(XYZ origin, XYZ right, XYZ up, XYZ normal)
        {
            Origin = origin;
            Right = right;
            Up = up;
            Normal = normal;
        }
    }
}
