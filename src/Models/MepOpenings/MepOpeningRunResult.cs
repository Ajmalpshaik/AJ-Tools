#region Metadata
/*
 * Tool Name     : MEP Openings
 * File Name     : MepOpeningRunResult.cs
 * Purpose       : Captures the final report data for a MEP opening run.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-03
 * Last Updated  : 2026-07-03
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : System.Collections.Generic
 *
 * Input         : Processing counters and skip reasons.
 * Output        : Plain-language report for Ajmal after the command runs.
 *
 * Notes         :
 * - The command reports counts and reasons; it does not use a bare success popup.
 *
 * Changelog     :
 * v1.0.0 (2026-07-03) - Initial release.
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

        public IEnumerable<string> GetTopSkipReasons()
        {
            return SkipReasons
                .GroupBy(reason => reason)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Take(8)
                .Select(group => group.Key + " (" + group.Count() + ")");
        }
    }
}
