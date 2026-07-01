#region Metadata
/*
 * Tool Name     : Graphics Tools (shared)
 * File Name     : GraphicsLineWeightOption.cs
 * Purpose       : Represents a line weight UI option for the graphics override settings dropdowns.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-03-30
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Line weight value and display name.
 * Output        : Display-ready line weight option item.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - The invalid pen number represents the "By View" option.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Full metadata block; reviewed for release.
 * v1.4.4 (2026-05-09) - Reviewed line weight option model for the updated Apply Graphics UI.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
