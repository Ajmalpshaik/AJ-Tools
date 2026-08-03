#region Metadata
/*
 * Tool Name     : Purge Unused Elements (shared model)
 * File Name     : UnusedElementPurgeResult.cs
 * Purpose       : Outcome of a Purge Unused Elements delete pass - counts plus per-item skip/failure reasons.
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
 * - Shaped after UnplacedViewPurgeResult/UnplacedViewPurgeIssue.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;

namespace AJTools.Models.Purge
{
    internal sealed class UnusedElementPurgeIssue
    {
        public UnusedElementPurgeIssue(string elementName, int elementIdValue, string reason)
        {
            ElementName = elementName ?? string.Empty;
            ElementIdValue = elementIdValue;
            Reason = reason ?? string.Empty;
        }

        public string ElementName { get; }

        public int ElementIdValue { get; }

        public string Reason { get; }
    }

    internal sealed class UnusedElementPurgeResult
    {
        public UnusedElementPurgeResult()
        {
            Skipped = new List<UnusedElementPurgeIssue>();
            Failures = new List<UnusedElementPurgeIssue>();
        }

        public int FoundCount { get; set; }

        public int AttemptedCount { get; set; }

        public int DeletedCount { get; set; }

        public IList<UnusedElementPurgeIssue> Skipped { get; }

        public IList<UnusedElementPurgeIssue> Failures { get; }

        public int SkippedCount
        {
            get { return Skipped.Count; }
        }

        public int FailedCount
        {
            get { return Failures.Count; }
        }

        public void AddSkipped(string elementName, int elementIdValue, string reason)
        {
            Skipped.Add(new UnusedElementPurgeIssue(elementName, elementIdValue, reason));
        }

        public void AddFailure(string elementName, int elementIdValue, string reason)
        {
            Failures.Add(new UnusedElementPurgeIssue(elementName, elementIdValue, reason));
        }
    }
}
