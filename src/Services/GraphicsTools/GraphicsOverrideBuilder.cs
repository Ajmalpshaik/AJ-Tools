// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Builds Revit OverrideGraphicSettings from graphics input values.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2026-03-30
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Graphics override input model.
// Output       : Revit OverrideGraphicSettings object.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.1.0 - Cleaned Graphics Tools command flow, shared validation/transaction handling, and metadata.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using Autodesk.Revit.DB;
using AJTools.Models.GraphicsTools;

namespace AJTools.Services.GraphicsTools
{
    /// <summary>
    /// Creates complete OverrideGraphicSettings objects from UI input.
    /// </summary>
    internal static class GraphicsOverrideBuilder
    {
        public static OverrideGraphicSettings Build(GraphicsOverrideInput input)
        {
            var safeInput = input ?? new GraphicsOverrideInput();
            var settings = new OverrideGraphicSettings();

            settings.SetProjectionLineColor(ToColorOrInvalid(safeInput.ProjectionLineColor));
            settings.SetProjectionLinePatternId(ToIdOrInvalid(safeInput.ProjectionLinePatternId));
            settings.SetProjectionLineWeight(NormalizeLineWeight(safeInput.ProjectionLineWeight));

            ElementId surfaceForegroundPatternId = ToIdOrInvalid(safeInput.SurfaceForegroundPatternId);
            settings.SetSurfaceForegroundPatternId(surfaceForegroundPatternId);
            settings.SetSurfaceForegroundPatternVisible(IsValidId(surfaceForegroundPatternId));
            settings.SetSurfaceForegroundPatternColor(ToColorOrInvalid(safeInput.SurfaceForegroundPatternColor));

            ElementId surfaceBackgroundPatternId = ToIdOrInvalid(safeInput.SurfaceBackgroundPatternId);
            settings.SetSurfaceBackgroundPatternId(surfaceBackgroundPatternId);
            settings.SetSurfaceBackgroundPatternVisible(IsValidId(surfaceBackgroundPatternId));
            settings.SetSurfaceBackgroundPatternColor(ToColorOrInvalid(safeInput.SurfaceBackgroundPatternColor));

            settings.SetSurfaceTransparency(ClampTransparency(safeInput.Transparency));

            settings.SetCutLineColor(ToColorOrInvalid(safeInput.CutLineColor));
            settings.SetCutLinePatternId(ToIdOrInvalid(safeInput.CutLinePatternId));
            settings.SetCutLineWeight(NormalizeLineWeight(safeInput.CutLineWeight));

            ElementId cutForegroundPatternId = ToIdOrInvalid(safeInput.CutForegroundPatternId);
            settings.SetCutForegroundPatternId(cutForegroundPatternId);
            settings.SetCutForegroundPatternVisible(IsValidId(cutForegroundPatternId));
            settings.SetCutForegroundPatternColor(ToColorOrInvalid(safeInput.CutForegroundPatternColor));

            ElementId cutBackgroundPatternId = ToIdOrInvalid(safeInput.CutBackgroundPatternId);
            settings.SetCutBackgroundPatternId(cutBackgroundPatternId);
            settings.SetCutBackgroundPatternVisible(IsValidId(cutBackgroundPatternId));
            settings.SetCutBackgroundPatternColor(ToColorOrInvalid(safeInput.CutBackgroundPatternColor));

            settings.SetHalftone(safeInput.Halftone);

            return settings;
        }

        public static OverrideGraphicSettings Clone(OverrideGraphicSettings source)
        {
            return source == null
                ? new OverrideGraphicSettings()
                : new OverrideGraphicSettings(source);
        }

        private static Color ToColorOrInvalid(GraphicsColorValue colorValue)
        {
            return colorValue == null
                ? Color.InvalidColorValue
                : colorValue.ToRevitColorOrInvalid();
        }

        private static ElementId ToIdOrInvalid(ElementId id)
        {
            return id ?? ElementId.InvalidElementId;
        }

        private static bool IsValidId(ElementId id)
        {
            return id != null && id != ElementId.InvalidElementId;
        }

        private static int NormalizeLineWeight(int lineWeight)
        {
            if (lineWeight == OverrideGraphicSettings.InvalidPenNumber)
            {
                return OverrideGraphicSettings.InvalidPenNumber;
            }

            if (lineWeight < 1)
            {
                return OverrideGraphicSettings.InvalidPenNumber;
            }

            if (lineWeight > 16)
            {
                return 16;
            }

            return lineWeight;
        }

        private static int ClampTransparency(int transparency)
        {
            if (transparency < 0)
            {
                return 0;
            }

            if (transparency > 100)
            {
                return 100;
            }

            return transparency;
        }
    }
}
