// ==================================================
// Tool Name    : Purge Unplaced Views
// Purpose      : Safety status of a scanned Purge Unplaced view row (Safe to Purge / Skipped / Cannot Delete).
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-11
// Last Updated : 2026-05-11
// Target       : Revit 2020 - 2027
// Framework    : .NET Framework 4.7.2 (2020) / 4.8 (2021-2024) / .NET 8 (2025-2026) / .NET 10 (2027)
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit document and user purge options.
// Output       : Safe purge result with final report.
// Notes        : Added under AJ Tools Purge panel.
// Changelog    : v1.0.0 - Ported from an earlier AJ Tools script.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using System;

namespace AJTools.Models.Purge
{
    internal enum UnplacedViewPurgeStatus
    {
        SafeToPurge = 0,
        Skipped = 1,
        CannotDelete = 2
    }

    internal static class UnplacedViewPurgeStatusExtensions
    {
        public static string ToDisplayText(this UnplacedViewPurgeStatus status)
        {
            switch (status)
            {
                case UnplacedViewPurgeStatus.SafeToPurge:
                    return "Safe to Purge";
                case UnplacedViewPurgeStatus.Skipped:
                    return "Skipped";
                case UnplacedViewPurgeStatus.CannotDelete:
                    return "Cannot Delete";
                default:
                    return Enum.GetName(typeof(UnplacedViewPurgeStatus), status) ?? "Unknown";
            }
        }
    }
}
