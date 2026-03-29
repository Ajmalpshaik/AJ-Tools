// Tool Name: Geometry Extensions
// Description: Extension methods for Revit geometry types (XYZ, Curve, etc.).
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-11
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
        /// Checks if an XYZ vector has near-zero length.
        /// </summary>
        /// <param name="vector">The vector to check.</param>
        /// <returns>True if the vector length is less than the zero tolerance.</returns>
        public static bool IsZeroLength(this XYZ vector)
        {
            return vector.GetLength() < Constants.ZERO_LENGTH_TOLERANCE;
        }

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
