// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Holds graphics settings input before building Revit overrides.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.2.0
// Created      : 2026-03-30
// Last Updated : 2026-05-07
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : WPF graphics settings selections.
// Output       : Structured graphics override input model.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.2.0 - Combined Apply Graphics workflow and corrected cut-link UI behavior.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using Autodesk.Revit.DB;

namespace AJTools.Models.GraphicsTools
{
    internal enum GraphicsApplyMode
    {
        SelectedElements = 0,
        Categories = 1
    }

    /// <summary>
    /// Input model used to build a complete OverrideGraphicSettings package.
    /// </summary>
    internal sealed class GraphicsOverrideInput
    {
        public GraphicsOverrideInput()
        {
            ProjectionLineColor = GraphicsColorValue.ByView();
            ProjectionLinePatternId = ElementId.InvalidElementId;
            ProjectionLineWeight = OverrideGraphicSettings.InvalidPenNumber;

            SurfaceForegroundPatternId = ElementId.InvalidElementId;
            SurfaceForegroundPatternColor = GraphicsColorValue.ByView();
            SurfaceBackgroundPatternId = ElementId.InvalidElementId;
            SurfaceBackgroundPatternColor = GraphicsColorValue.ByView();

            Transparency = 0;

            CutLineColor = GraphicsColorValue.ByView();
            CutLinePatternId = ElementId.InvalidElementId;
            CutLineWeight = OverrideGraphicSettings.InvalidPenNumber;

            CutForegroundPatternId = ElementId.InvalidElementId;
            CutForegroundPatternColor = GraphicsColorValue.ByView();
            CutBackgroundPatternId = ElementId.InvalidElementId;
            CutBackgroundPatternColor = GraphicsColorValue.ByView();

            Halftone = false;
            ApplyMode = GraphicsApplyMode.SelectedElements;
            UseProjectionSurfaceSettingsForCut = false;
        }

        public GraphicsColorValue ProjectionLineColor { get; set; }

        public ElementId ProjectionLinePatternId { get; set; }

        public int ProjectionLineWeight { get; set; }

        public ElementId SurfaceForegroundPatternId { get; set; }

        public GraphicsColorValue SurfaceForegroundPatternColor { get; set; }

        public ElementId SurfaceBackgroundPatternId { get; set; }

        public GraphicsColorValue SurfaceBackgroundPatternColor { get; set; }

        public int Transparency { get; set; }

        public GraphicsColorValue CutLineColor { get; set; }

        public ElementId CutLinePatternId { get; set; }

        public int CutLineWeight { get; set; }

        public ElementId CutForegroundPatternId { get; set; }

        public GraphicsColorValue CutForegroundPatternColor { get; set; }

        public ElementId CutBackgroundPatternId { get; set; }

        public GraphicsColorValue CutBackgroundPatternColor { get; set; }

        public bool Halftone { get; set; }

        public GraphicsApplyMode ApplyMode { get; set; }

        public bool UseProjectionSurfaceSettingsForCut { get; set; }
    }
}
