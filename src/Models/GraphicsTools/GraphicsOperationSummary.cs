// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Tracks attempted, applied, and skipped graphics operations.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.4.4
// Created      : 2026-03-30
// Last Updated : 2026-05-09
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Graphics operation result counts.
// Output       : Summary state for transaction decisions.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.4.4 - Reviewed summary model for shared Graphics transaction handling.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

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
