#region Metadata
/*
 * Tool Name     : AJ-Tools
 * File Name     : Constants.cs
 * Purpose       : Centralized numeric constants shared across all AJ-Tools — unit conversions,
 *                 geometry tolerances, and scan limits.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.1
 *
 * Created Date  : 2025-12-11
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : None
 *
 * Input         : N/A — constants only
 * Output        : N/A — constants only
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - No Revit API dependency; framework-agnostic.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-11) - Initial release.
 * v1.0.1 (2026-06-30) - Added mandatory metadata block; confirmed 2020-latest version coverage.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJTools.Utils
{
    /// <summary>
    /// Provides centralized constant values used throughout the application.
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// Conversion factor from millimeters to feet (Revit internal units).
        /// </summary>
        public const double MM_TO_FEET = 0.00328084;

        /// <summary>
        /// Conversion factor from feet (Revit internal units) to millimeters.
        /// </summary>
        public const double FEET_TO_MM = 304.8;

        /// <summary>
        /// Minimum distance threshold for point comparisons and geometry validation.
        /// </summary>
        public const double MIN_DISTANCE_TOLERANCE = 0.001;

        /// <summary>
        /// Tolerance for parallel direction vector comparisons.
        /// </summary>
        public const double PARALLEL_TOLERANCE = 0.001;

        /// <summary>
        /// Maximum number of elements to scan when collecting parameter values.
        /// Prevents performance issues in large models.
        /// </summary>
        public const int ELEMENT_SCAN_LIMIT = 10000;

        /// <summary>
        /// Precision for rounding grid direction keys (decimal places).
        /// </summary>
        public const int DIRECTION_PRECISION = 3;

        /// <summary>
        /// Very small value used for near-zero length checks.
        /// </summary>
        public const double ZERO_LENGTH_TOLERANCE = 1e-9;

        /// <summary>
        /// Epsilon for elevation comparisons.
        /// </summary>
        public const double ELEVATION_EPSILON = 1e-6;
    }
}
