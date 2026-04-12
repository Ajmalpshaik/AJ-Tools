using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AJTools.Services.RevisionCloud
{
    /// <summary>
    /// Creates Revit RevisionCloud elements from orthogonal polygon boundaries.
    /// Passes straight-line edges to Revit, which applies its own cloud arc styling
    /// based on the project's revision cloud arc length setting.
    /// </summary>
    internal static class RevisionCloudCreator
    {
        /// <summary>
        /// Creates a revision cloud in the given view from an orthogonal polygon defined in UV space.
        /// </summary>
        public static Autodesk.Revit.DB.RevisionCloud Create(
            Document doc,
            View view,
            ViewPlaneData viewPlane,
            List<UV> polygon,
            ElementId revisionId)
        {
            if (polygon == null || polygon.Count < 4)
                return null;

            var curves = BuildLineCurves(polygon, viewPlane);
            if (curves == null || curves.Count == 0)
                return null;

            return Autodesk.Revit.DB.RevisionCloud.Create(doc, view, revisionId, curves);
        }

        /// <summary>
        /// Converts a UV polygon into Line curves on the view plane.
        /// Revit handles the cloud arc rendering automatically.
        /// </summary>
        private static IList<Curve> BuildLineCurves(List<UV> polygon, ViewPlaneData plane)
        {
            var curves = new List<Curve>();
            int n = polygon.Count;

            for (int i = 0; i < n; i++)
            {
                XYZ p0 = GeometryProjectionService.UnprojectFromViewPlane(polygon[i], plane);
                XYZ p1 = GeometryProjectionService.UnprojectFromViewPlane(polygon[(i + 1) % n], plane);

                if (p0.DistanceTo(p1) < 1e-9)
                    continue;

                try
                {
                    curves.Add(Line.CreateBound(p0, p1));
                }
                catch
                {
                    // Skip degenerate segments
                }
            }

            return curves.Count >= 3 ? curves : null;
        }
    }
}
