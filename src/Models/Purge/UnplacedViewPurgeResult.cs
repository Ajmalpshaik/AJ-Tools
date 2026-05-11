// ==================================================
// Tool Name    : Purge Unplaced 3D Views and Sections
// Purpose      : Convert Python shell purge workflow into AJ Tools C# Revit add-in.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-11
// Last Updated : 2026-05-11
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit document and user purge options.
// Output       : Safe purge result with final report.
// Notes        : Added under AJ Tools Purge panel.
// Changelog    : v1.0.0 - Converted from Interactive Python Shell script.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using System.Collections.Generic;

namespace AJTools.Models.Purge
{
    internal sealed class UnplacedViewPurgeIssue
    {
        public UnplacedViewPurgeIssue(string viewName, int viewIdValue, string reason)
        {
            ViewName = viewName ?? string.Empty;
            ViewIdValue = viewIdValue;
            Reason = reason ?? string.Empty;
        }

        public string ViewName { get; }

        public int ViewIdValue { get; }

        public string Reason { get; }
    }

    internal sealed class UnplacedViewPurgeResult
    {
        public UnplacedViewPurgeResult()
        {
            Skipped = new List<UnplacedViewPurgeIssue>();
            Failures = new List<UnplacedViewPurgeIssue>();
        }

        public int FoundCount { get; set; }

        public int AttemptedCount { get; set; }

        public int DeletedCount { get; set; }

        public IList<UnplacedViewPurgeIssue> Skipped { get; }

        public IList<UnplacedViewPurgeIssue> Failures { get; }

        public int SkippedCount
        {
            get { return Skipped.Count; }
        }

        public int FailedCount
        {
            get { return Failures.Count; }
        }

        public void AddSkipped(string viewName, int viewIdValue, string reason)
        {
            Skipped.Add(new UnplacedViewPurgeIssue(viewName, viewIdValue, reason));
        }

        public void AddFailure(string viewName, int viewIdValue, string reason)
        {
            Failures.Add(new UnplacedViewPurgeIssue(viewName, viewIdValue, reason));
        }
    }
}
