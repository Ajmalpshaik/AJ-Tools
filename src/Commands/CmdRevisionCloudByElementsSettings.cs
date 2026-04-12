using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.RevisionCloud;

namespace AJTools.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdRevisionCloudByElementsSettings : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var settings = RevisionCloudSettings.Load() ?? new RevisionCloudSettings();
                if (double.IsNaN(settings.OffsetDistanceMm) || double.IsInfinity(settings.OffsetDistanceMm) || settings.OffsetDistanceMm < 0)
                    settings.OffsetDistanceMm = 50.0;

                var settingsWindow = new UI.RevisionCloud.RevisionCloudSettingsWindow(settings);
                settingsWindow.ShowDialog();

                return settingsWindow.Confirmed ? Result.Succeeded : Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Revision Cloud By Elements - Settings Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
