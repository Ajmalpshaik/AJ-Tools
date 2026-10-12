// Tool Name: Geometry Extensions
// Description: Extension methods for Revit geometry types (XYZ, Curve, etc.).
// Author: Ajmal P.S.
// Version: 1.0.1
// Last Updated: 2026-10-12
// Changes: 1.0.1 - removed the XYZ.IsZeroLength() extension; Revit's own XYZ.IsZeroLength() instance
//          method always wins over an extension of the same name, so it was never called.
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using Autodesk.Revit.DB;

namespace AJTools.Utils
{
    /// <summary>
    /// Provides extension methods for common geometry operations.
    /// </summary>
    internal static class GeometryExtensions
    {

        /// <summary>
        /// Gets the normalized direction of a curve.
        /// Returns null if the curve direction cannot be determined.
        /// </summary>
        /// <param name="curve">The curve to analyze.</param>
        /// <returns>Normalized direction vector, or null if indeterminate.</returns>
        public static XYZ GetCurveDirection(this Curve curve)
        {
            if (curve == null)
                return null;

            if (curve is Line line)
            {
                XYZ dir = line.Direction;
                return dir.IsZeroLength() ? null : dir.Normalize();
            }

            if (curve.IsBound)
            {
                XYZ p0 = curve.GetEndPoint(0);
                XYZ p1 = curve.GetEndPoint(1);
                XYZ vec = p1 - p0;

                if (vec.IsZeroLength())
                    return null;

                return vec.Normalize();
            }

            return null;
        }
    }
}
