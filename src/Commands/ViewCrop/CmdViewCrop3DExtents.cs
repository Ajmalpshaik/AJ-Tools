// ==================================================
// Tool Name    : View Crop
// Purpose      : External commands for View Crop and annotation crop workflows.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.1
// Created      : 2026-04-08
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API, WPF
// Input        : Active Revit document, active or selected target views, and View Crop settings.
// Output       : Updated view crop or annotation crop settings for supported target views.
// Notes        : Skips unsupported, template, scope-box-controlled, and view-template-locked views.
// Changelog    : v1.0.1 - Standardized metadata after production cleanup.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
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
