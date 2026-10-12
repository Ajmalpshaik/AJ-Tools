#region Metadata
/*
 * Tool Name     : MEP Openings
 * File Name     : MepOpeningRunResult.cs
 * Purpose       : Captures the final report data for a MEP opening run.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.1
 *
 * Created Date  : 2026-07-03
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : System.Collections.Generic
 *
 * Input         : Processing counters, skip reasons and failure reasons.
 * Output        : Plain-language report for Ajmal after the command runs.
 *
 * Notes         :
 * - The command reports counts and reasons; it does not use a bare success popup.
 *
 * Changelog     :
 * v1.0.0 (2026-07-03) - Initial release.
 * v1.0.1 (2026-10-12) - Added GetTopFailureReasons (same grouping and cap as the skip reasons, shared
 *                       through one helper) for the Create Openings report - the failure reasons were
 *                       collected but never shown anywhere.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using System.Linq;

namespace AJTools.Models.MepOpenings
{
    public sealed class MepOpeningRunResult
    {
        public int SelectedCount { get; set; }
        public int SupportedMepCount { get; set; }
        public int HostIntersectionsChecked { get; set; }
        public int OpeningRequests { get; set; }
        public int OpeningsCreated { get; set; }
        public int ExistingOpeningsReplaced { get; set; }
        public int AlreadyCovered { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }

        public List<string> SkipReasons { get; } = new List<string>();

        public List<string> FailureReasons { get; } = new List<string>();

        public void AddSkip(string reason)
        {
            Skipped++;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                SkipReasons.Add(reason);
            }
        }

        public void AddFailure(string reason)
        {
            Failed++;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                FailureReasons.Add(reason);
            }
        }

        /// <summary>
        /// The most reason lines a report shows per list; identical reasons are merged with a count.
        /// </summary>
        public const int MaxReportedReasons = 8;

        public IEnumerable<string> GetTopSkipReasons()
        {
            return GetTopReasons(SkipReasons);
        }

        public IEnumerable<string> GetTopFailureReasons()
        {
            return GetTopReasons(FailureReasons);
        }

        private static IEnumerable<string> GetTopReasons(IEnumerable<string> reasons)
        {
            return reasons
                .GroupBy(reason => reason)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Take(MaxReportedReasons)
                .Select(group => group.Key + " (" + group.Count() + ")");
        }
    }
}
