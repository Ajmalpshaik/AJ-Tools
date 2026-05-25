// ==================================================
// Tool Name    : Section Mark Visibility
// Purpose      : Model class for Section Mark Visibility settings.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-24
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// ==================================================

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
