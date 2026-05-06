// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Represents line weight UI options for override settings.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2026-03-30
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Line weight value and display name.
// Output       : Display-ready line weight option item.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.1.0 - Cleaned Graphics Tools command flow, shared validation/transaction handling, and metadata.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using Autodesk.Revit.DB;

namespace AJTools.Models.GraphicsTools
{
    /// <summary>
    /// Represents line weight options for OverrideGraphicSettings.
    /// </summary>
    internal sealed class GraphicsLineWeightOption
    {
        public GraphicsLineWeightOption(int weight, string displayName)
        {
            Weight = weight;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;
        }

        public int Weight { get; }

        public string DisplayName { get; }

        public bool IsByView
        {
            get { return Weight == OverrideGraphicSettings.InvalidPenNumber; }
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
