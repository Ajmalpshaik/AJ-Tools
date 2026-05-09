// ==================================================
// Tool Name    : Apply Graphics
// Purpose      : Stores last-used Apply Graphics UI choices for persistence.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.4.4
// Created      : 2026-05-09
// Last Updated : 2026-05-09
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : None
// Input        : Serializable graphics UI selections.
// Output       : Last-used graphics settings restored into the UI.
// Notes        : Stores UI choices only; Revit OverrideGraphicSettings are still built by GraphicsOverrideBuilder.
// Changelog    : v1.4.4 - Added last-used settings memory for the Graphics Tool.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using System.Collections.Generic;

namespace AJTools.Models.GraphicsTools
{
    /// <summary>
    /// Serializable state used to remember the last Apply Graphics selections.
    /// </summary>
    internal sealed class GraphicsOverrideMemoryState
    {
        public GraphicsOverrideMemoryState()
        {
            Version = 1;
            LastApplyMode = GraphicsApplyMode.SelectedElements;
            UseProjectionSurfaceSettingsForCut = false;
            Halftone = false;
            Transparency = 0;

            ProjectionLineColor = GraphicsColorMemoryValue.ByView();
            ProjectionLinePattern = GraphicsIdMemoryValue.ByView();
            ProjectionLineWeight = -1;

            SurfaceForegroundPattern = GraphicsIdMemoryValue.ByView();
            SurfaceForegroundColor = GraphicsColorMemoryValue.ByView();
            SurfaceBackgroundPattern = GraphicsIdMemoryValue.ByView();
            SurfaceBackgroundColor = GraphicsColorMemoryValue.ByView();

            CutLineColor = GraphicsColorMemoryValue.ByView();
            CutLinePattern = GraphicsIdMemoryValue.ByView();
            CutLineWeight = -1;

            CutForegroundPattern = GraphicsIdMemoryValue.ByView();
            CutForegroundColor = GraphicsColorMemoryValue.ByView();
            CutBackgroundPattern = GraphicsIdMemoryValue.ByView();
            CutBackgroundColor = GraphicsColorMemoryValue.ByView();

            HasCategorySelection = false;
            SelectedCategoryIntegerIds = new List<int>();
            SelectedCategoryNames = new List<string>();
        }

        public int Version { get; set; }

        public GraphicsApplyMode LastApplyMode { get; set; }

        public bool UseProjectionSurfaceSettingsForCut { get; set; }

        public bool Halftone { get; set; }

        public int Transparency { get; set; }

        public GraphicsColorMemoryValue ProjectionLineColor { get; set; }

        public GraphicsIdMemoryValue ProjectionLinePattern { get; set; }

        public int ProjectionLineWeight { get; set; }

        public GraphicsIdMemoryValue SurfaceForegroundPattern { get; set; }

        public GraphicsColorMemoryValue SurfaceForegroundColor { get; set; }

        public GraphicsIdMemoryValue SurfaceBackgroundPattern { get; set; }

        public GraphicsColorMemoryValue SurfaceBackgroundColor { get; set; }

        public GraphicsColorMemoryValue CutLineColor { get; set; }

        public GraphicsIdMemoryValue CutLinePattern { get; set; }

        public int CutLineWeight { get; set; }

        public GraphicsIdMemoryValue CutForegroundPattern { get; set; }

        public GraphicsColorMemoryValue CutForegroundColor { get; set; }

        public GraphicsIdMemoryValue CutBackgroundPattern { get; set; }

        public GraphicsColorMemoryValue CutBackgroundColor { get; set; }

        public bool HasCategorySelection { get; set; }

        public List<int> SelectedCategoryIntegerIds { get; set; }

        public List<string> SelectedCategoryNames { get; set; }
    }

    internal sealed class GraphicsColorMemoryValue
    {
        public bool IsByView { get; set; }

        public byte Red { get; set; }

        public byte Green { get; set; }

        public byte Blue { get; set; }

        public static GraphicsColorMemoryValue ByView()
        {
            return new GraphicsColorMemoryValue
            {
                IsByView = true,
                Red = 0,
                Green = 0,
                Blue = 0
            };
        }

        public static GraphicsColorMemoryValue FromGraphicsColor(GraphicsColorValue color)
        {
            if (color == null || color.IsByView)
            {
                return ByView();
            }

            return new GraphicsColorMemoryValue
            {
                IsByView = false,
                Red = color.Red,
                Green = color.Green,
                Blue = color.Blue
            };
        }

        public GraphicsColorValue ToGraphicsColor()
        {
            return IsByView
                ? GraphicsColorValue.ByView()
                : GraphicsColorValue.FromRgb(Red, Green, Blue);
        }
    }

    internal sealed class GraphicsIdMemoryValue
    {
        public int IntegerValue { get; set; }

        public string DisplayName { get; set; }

        public static GraphicsIdMemoryValue ByView()
        {
            return new GraphicsIdMemoryValue
            {
                IntegerValue = -1,
                DisplayName = "<By View>"
            };
        }
    }
}
