// Tool Name: Smart MEP Tag Command
// Description: Analyses the active view and intelligently tags MEP elements with clash-free placement.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Services.SmartTag

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.SmartTag;

namespace AJTools.Commands
{
    /// <summary>
    /// Analyses the active Revit view, collects MEP elements, and places tags intelligently
    /// using a scoring engine and clash detection — like an experienced BIM modeller would.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdSmartMepTag : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return SmartMepTagService.Execute(commandData, ref message);
        }
    }
}
