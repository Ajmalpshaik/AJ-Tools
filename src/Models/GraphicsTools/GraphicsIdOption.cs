#region Metadata
/*
 * Tool Name     : Graphics Tools (shared)
 * File Name     : GraphicsIdOption.cs
 * Purpose       : Represents an ElementId-based UI option (line/fill pattern) for the graphics settings dropdowns.
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
 * Input         : Revit ElementId and display name.
 * Output        : Display-ready graphics option item.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - InvalidElementId represents the "By View" option.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Full metadata block; reviewed for release.
 * v1.4.4 (2026-05-09) - Reviewed ElementId option model for persisted Apply Graphics settings.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;

namespace AJTools.Models.GraphicsTools
{
    /// <summary>
    /// Generic ElementId option item used by graphics settings dropdowns.
    /// </summary>
    internal sealed class GraphicsIdOption
    {
        public GraphicsIdOption(ElementId id, string displayName)
        {
            Id = id ?? ElementId.InvalidElementId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;
        }

        public ElementId Id { get; }

        public string DisplayName { get; }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
