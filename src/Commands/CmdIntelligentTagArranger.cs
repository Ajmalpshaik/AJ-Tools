// Tool Name: Intelligent Tag Arranger Command
// Description: Rearranges selected tags into a vertical stack using nearest-first T1/L1 logic.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-07
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Services.TagArrange

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.TagArrange;

namespace AJTools.Commands
{
    /// <summary>
    /// Rearranges pre-selected IndependentTags into a clean vertical stack.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdIntelligentTagArranger : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return IntelligentTagArrangerService.Execute(commandData, ref message);
        }
    }
}
