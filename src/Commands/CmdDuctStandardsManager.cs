using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI.DuctStandards;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdDuctStandardsManager : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            var window = new DuctStandardsManagerWindow(uidoc);
            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
