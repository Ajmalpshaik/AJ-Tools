#region Metadata
/*
 * Tool Name     : AJ Tools Shared Helper
 * File Name     : CeilingGridApiCompat.cs
 * Purpose       : Safe, version-gated access to Ceiling.GetCeilingGridLines (Revit 2025.3+).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-07
 * Last Updated  : 2026-07-07
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / .NET Fx 4.8 (2021-2024) | .NET 8 (2025-2026) | .NET 10 (2027 - verify SDK)
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Ceiling.
 * Output         : The ceiling's real grid line geometry (not the boundary), or null if unavailable.
 *
 * Notes         :
 * - Ceiling.GetCeilingGridLines(bool includeBoundary) was added in Revit 2025.3, not from the start of
 *   Revit 2025. AJ-Tools compiles against the Nice3point NuGet reference package (Version="2025.*"),
 *   which resolves to a version that already has this method - so the R25/R26/R27 builds COMPILE fine.
 *   But at RUNTIME, Revit itself supplies the real RevitAPI.dll (the NuGet package is
 *   ExcludeAssets="runtime" - reference-only). A user still on an unpatched Revit 2025.0-2025.2
 *   installation would NOT have this method at runtime, even though the add-in compiled against a
 *   newer reference. A plain compile-time `#if REVIT2025_OR_GREATER` guard is therefore NOT enough on
 *   its own - it must also be checked for existence at runtime before calling it.
 * - This helper does both: compiles the call out entirely below Revit 2025 (no reflection cost at all
 *   on 2020-2024), and on 2025+ checks via reflection whether the method actually exists on the
 *   installed Revit's Ceiling type before invoking it. If it doesn't exist (or the call throws for any
 *   reason), the caller receives false/null and is expected to fall back to its existing method - this
 *   NEVER causes a crash, only a silent fallback to the old, already-proven behaviour.
 *
 * Changelog     :
 * v1.0.0 (2026-07-07) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.DB;

namespace AJTools.Utils
{
    /// <summary>
    /// Safe, version-gated access to Ceiling.GetCeilingGridLines (Revit 2025.3+). Never throws -
    /// returns false when the method is unavailable on the running Revit version, so callers can
    /// fall back to their existing detection method.
    /// </summary>
    internal static class CeilingGridApiCompat
    {
#if REVIT2025_OR_GREATER
        private static readonly MethodInfo GridLinesMethod =
            typeof(Ceiling).GetMethod("GetCeilingGridLines", new[] { typeof(bool) });
#endif

        /// <summary>
        /// Attempts to read the ceiling's real grid line geometry (excluding the ceiling boundary).
        /// Returns false (with <paramref name="lines"/> null) if the API is unavailable on the running
        /// Revit version, or the ceiling has no grid lines to report.
        /// </summary>
        internal static bool TryGetGridLines(Ceiling ceiling, out IList<Curve> lines)
        {
            lines = null;
            if (ceiling == null)
                return false;

#if REVIT2025_OR_GREATER
            if (GridLinesMethod == null)
                return false;

            try
            {
                lines = GridLinesMethod.Invoke(ceiling, new object[] { false }) as IList<Curve>;
                return lines != null && lines.Count > 0;
            }
            catch
            {
                lines = null;
                return false;
            }
#else
            return false;
#endif
        }
    }
}
