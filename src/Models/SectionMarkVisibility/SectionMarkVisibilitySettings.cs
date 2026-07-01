#region Metadata
/*
 * Tool Name     : Section Mark Visibility
 * File Name     : SectionMarkVisibilitySettings.cs
 * Purpose       : Settings model — run scope, sheet-number filter, and mode flags.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.2.0
 *
 * Created Date  : 2026-05-24
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : —
 *
 * Input         : —
 * Output        : —
 *
 * Changelog     :
 * v1.0.0 (2026-05-24) - Initial release.
 * v1.2.0 (2026-06-30) - Cleanup pass: metadata block.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;

namespace AJTools.Models.SectionMarkVisibility
{
    /// <summary>
    /// Holds the user preferences and filters for Section Mark Visibility operations.
    /// </summary>
    internal sealed class SectionMarkVisibilitySettings
    {
        /// <summary>
        /// True if only the active plan view is processed; false for selected plan views.
        /// </summary>
        public bool ApplyToActiveViewOnly { get; set; } = true;

        /// <summary>
        /// List of user-entered sheet numbers to filter sections.
        /// </summary>
        public List<string> SheetNumbers { get; set; } = new List<string>();

        /// <summary>
        /// True if Mode 2 (Keep All Placed Sections) is active.
        /// </summary>
        public bool KeepAllPlacedSections { get; set; } = false;

        /// <summary>
        /// True if Mode 3 (Unhide All Sections) is active. Only unhides, no filtering.
        /// </summary>
        public bool UnhideAllSections { get; set; } = false;

        /// <summary>
        /// Creates a deep copy of the settings instance.
        /// </summary>
        public SectionMarkVisibilitySettings Clone()
        {
            return new SectionMarkVisibilitySettings
            {
                ApplyToActiveViewOnly = this.ApplyToActiveViewOnly,
                KeepAllPlacedSections = this.KeepAllPlacedSections,
                UnhideAllSections = this.UnhideAllSections,
                SheetNumbers = new List<string>(this.SheetNumbers)
            };
        }
    }
}
