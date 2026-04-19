using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI.Purge;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdPurgeUnusedFamilyParameters : IExternalCommand
    {
        private const string ToolTitle = "Purge Unused Family Parameters";
        private const string FamilyOnlyMessage = "This tool works only in an opened Revit Family file.";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData?.Application?.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                TaskDialog.Show(ToolTitle, FamilyOnlyMessage);
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;
            if (!doc.IsFamilyDocument)
            {
                TaskDialog.Show(ToolTitle, FamilyOnlyMessage);
                return Result.Cancelled;
            }

            var window = new PurgeUnusedFamilyParametersWindow(doc);
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
