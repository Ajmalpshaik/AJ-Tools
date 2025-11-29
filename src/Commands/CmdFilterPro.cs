using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools
{
    /// <summary>
    /// Command to open the Filter Pro window for creating and managing Revit filters.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdFilterPro : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                TaskDialog.Show("Filter Pro", "Open a project document before running this command.");
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;
            View activeView = uiDoc.ActiveView;

            var window = new FilterProWindow(doc, activeView);
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
