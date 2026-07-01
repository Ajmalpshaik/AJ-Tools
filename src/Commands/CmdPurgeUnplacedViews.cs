#region Metadata
/*
 * Tool Name     : Purge Unplaced Views (shared runner)
 * File Name     : CmdPurgeUnplacedViews.cs
 * Purpose       : Shared runner used by the Purge Unplaced 3D Views and Purge Unplaced Sections commands;
 *                 validates the project, opens the preview window for the chosen mode, and reports the result.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-05-11
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.Purge, AJTools.UI.Purge (PurgeUnplacedViewsWindow)
 *
 * Input         : Full Project - the purge mode (3D views or sections) passed by the calling command.
 * Output        : Selected unplaced views deleted (transaction owned by the window); Succeeded/Cancelled.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Project-only; exits cleanly in the Family Editor.
 * - Not exposed as its own ribbon button - it is the shared engine behind the two purge commands.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-05-11) - Converted from interactive Python shell script.
 * v1.1.0 (2026-07-01) - Refactor/audit: standardized metadata block. Purge behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.Purge;
using AJTools.UI.Purge;

namespace AJTools.Commands
{
    internal static class UnplacedViewPurgeCommandRunner
    {
        private const string ProjectOnlyMessage = "This tool works only in an opened Revit project file.";

        public static Result Execute(
            ExternalCommandData commandData,
            ref string message,
            UnplacedViewPurgeMode mode)
        {
            string toolTitle = mode.GetToolTitle();
            try
            {
                UIDocument uiDoc = commandData?.Application?.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document == null)
                {
                    TaskDialog.Show(toolTitle, ProjectOnlyMessage);
                    return Result.Cancelled;
                }

                Document doc = uiDoc.Document;
                if (doc.IsFamilyDocument)
                {
                    TaskDialog.Show(toolTitle, ProjectOnlyMessage);
                    return Result.Cancelled;
                }

                ElementId activeViewId = uiDoc.ActiveView != null
                    ? uiDoc.ActiveView.Id
                    : ElementId.InvalidElementId;

                var window = new PurgeUnplacedViewsWindow(doc, activeViewId, mode);
                window.ShowDialog();

                return window.OperationWasRun ? Result.Succeeded : Result.Cancelled;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show(toolTitle, "Tool failed:\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
