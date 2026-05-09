// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Represents ElementId-based UI options for graphics settings.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.4.4
// Created      : 2026-03-30
// Last Updated : 2026-05-09
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Revit ElementId and display name.
// Output       : Display-ready graphics option item.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.4.4 - Reviewed ElementId option model for persisted Apply Graphics settings.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

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
