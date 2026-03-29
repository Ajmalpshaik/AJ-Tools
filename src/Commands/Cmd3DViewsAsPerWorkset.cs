// Tool Name: 3D Views as per Workset
// Description: Creates one 3D view per user workset and isolates each matching workset.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-03-24
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Services.WorksetViews

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.WorksetViews;

namespace AJTools.Commands
{
    /// <summary>
    /// Creates 3D views named after user worksets and isolates each workset in its own view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class Cmd3DViewsAsPerWorkset : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Workset3DViewService.Execute(commandData, ref message);
        }
    }
}
