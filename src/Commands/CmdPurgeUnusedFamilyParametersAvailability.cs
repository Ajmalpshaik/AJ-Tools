using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    public class CmdPurgeUnusedFamilyParametersAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            UIDocument uiDoc = applicationData?.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                return false;
            }

            Document doc = uiDoc.Document;
            return doc.IsFamilyDocument;
        }
    }
}
