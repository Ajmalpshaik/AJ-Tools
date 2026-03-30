using Autodesk.Revit.DB;

namespace AJTools.Models.GraphicsTools
{
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
    }
}
