// Tool Name: Quick Parallel Dimension Command
// Description: Entry point for creating quick dimensions across selected parallel elements.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-03-29
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Services

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.QuickDimension;

namespace AJTools.Commands
{
    /// <summary>
    /// Backward-compatible quick parallel command (defaults to center line mode).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdQuickParallelDimension : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return QuickParallelDimensionService.Execute(commandData, QuickDimensionReferenceMode.Centerline);
        }
    }

    /// <summary>
    /// Creates quick dimensions using center line references.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdQuickParallelCenterLineDimension : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return QuickParallelDimensionService.Execute(commandData, QuickDimensionReferenceMode.Centerline);
        }
    }

    /// <summary>
    /// Creates quick dimensions using both side faces/edges where available.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdQuickParallelFaceEdgeDimension : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return QuickParallelDimensionService.Execute(commandData, QuickDimensionReferenceMode.FaceEdge);
        }
    }
}
