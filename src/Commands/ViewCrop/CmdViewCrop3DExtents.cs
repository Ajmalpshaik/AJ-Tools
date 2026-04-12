// Tool Name: View Crop Commands
// Description: External commands for fitting view crop extents and setting annotation crop offsets.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-08
// Revit Version: 2020

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.ViewCrop;
using AJTools.Services.ViewCrop;

namespace AJTools.Commands
{
    /// <summary>
    /// Fits crop by elements visible in each target view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdViewCropByActiveViewElements : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return ViewCropCommandService.Execute(
                commandData,
                ViewCropExtentSource.ActiveViewElements,
                "View Crop by Active View Elements",
                ref message);
        }
    }

    /// <summary>
    /// Fits crop by all model elements projected to each target view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdViewCropByAllModelElements : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return ViewCropCommandService.Execute(
                commandData,
                ViewCropExtentSource.AllModelElements,
                "View Crop by All Model Elements",
                ref message);
        }
    }

    /// <summary>
    /// Enables annotation crop and sets annotation offsets from the view crop boundary.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdSetAnnotationCropByViewCrop : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return ViewCropAnnotationCommandService.Execute(
                commandData,
                "Set Annotation Crop by View Crop",
                ref message);
        }
    }
}
