#region Metadata
/*
 * Tool Name     : Graphics Tools (shared)
 * File Name     : GraphicsOperationSummary.cs
 * Purpose       : Tracks attempted, applied, and skipped counts for a graphics override operation.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-03-30
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : None
 *
 * Input         : Graphics operation result counts.
 * Output        : Summary state used for transaction commit/rollback decisions and reporting.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - HasChanges is true only when at least one override was applied.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Full metadata block; reviewed for release.
 * v1.4.4 (2026-05-09) - Reviewed summary model for shared Graphics transaction handling.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJTools.Models.GraphicsTools
{
    /// <summary>
    /// Tracks attempted/applied/skipped counts for graphics operations.
    /// </summary>
    internal sealed class GraphicsOperationSummary
    {
        public int Attempted { get; set; }

        public int Applied { get; set; }

        public int Skipped { get; set; }

        public bool HasChanges
        {
            get { return Applied > 0; }
        }
    }
}
