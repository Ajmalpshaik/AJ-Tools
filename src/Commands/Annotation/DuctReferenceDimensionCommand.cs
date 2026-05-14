// Tool Name: Duct Reference Dimension Command
// Description: External command entry point for AJ Annotation duct reference dimensions.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-05-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Services

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.DuctReferenceDimension;

namespace AJTools.Commands.Annotation
{
    [Transaction(TransactionMode.Manual)]
    public class DuctReferenceDimensionCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return DuctReferenceDimensionService.Execute(commandData);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class DuctReferenceDimensionActiveViewCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return DuctReferenceDimensionService.ExecuteActiveView(commandData);
        }
    }
}
