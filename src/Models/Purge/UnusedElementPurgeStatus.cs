#region Metadata
/*
 * Tool Name     : Purge Unused Elements (shared model)
 * File Name     : UnusedElementPurgeStatus.cs
 * Purpose       : Tri-state safety status for a scanned UnusedElementPurgeItem row.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-07-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : None
 *
 * Input         : N/A
 * Output        : N/A
 *
 * Notes         :
 * - Deliberately parallel to UnplacedViewPurgeStatus rather than shared - keeps the two purge families
 *   (not-on-a-sheet vs not-referenced-anywhere) decoupled so a future change to one can't silently affect
 *   the other.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;

namespace AJTools.Models.Purge
{
    internal enum UnusedElementPurgeStatus
    {
        SafeToPurge = 0,
        Skipped = 1,
        CannotDelete = 2
    }

    internal static class UnusedElementPurgeStatusExtensions
    {
        public static string ToDisplayText(this UnusedElementPurgeStatus status)
        {
            switch (status)
            {
                case UnusedElementPurgeStatus.SafeToPurge:
                    return "Safe to Purge";
                case UnusedElementPurgeStatus.Skipped:
                    return "Skipped";
                case UnusedElementPurgeStatus.CannotDelete:
                    return "Cannot Delete";
                default:
                    return Enum.GetName(typeof(UnusedElementPurgeStatus), status) ?? "Unknown";
            }
        }
    }
}
