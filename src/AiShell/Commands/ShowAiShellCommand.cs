using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.AiShell.DockablePane;

namespace AJTools.AiShell.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowAiShellCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var paneId = AiShellPaneProvider.PaneId;
                var dockablePane = commandData.Application.GetDockablePane(paneId);
                
                if (dockablePane != null)
                {
                    if (dockablePane.IsShown())
                    {
                        dockablePane.Hide();
                    }
                    else
                    {
                        dockablePane.Show();
                    }
                }

                return Result.Succeeded;
            }
            catch (InvalidOperationException)
            {
                message = "C# pane is not registered. Please restart Revit.";
                return Result.Failed;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
