#region Metadata
/*
 * Tool Name     : Colorize
 * File Name     : CmdColorize.cs
 * Purpose       : Entry point command - validates context and opens the Colorize window. The window
 *                 itself applies each Shuffle Colors click directly (its own transaction, same as
 *                 Filter Pro), so this command only reports whether any click made changes.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-13
 * Last Updated  : 2026-07-13
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active View (must be a Filter Pro-capable view type - Plan, Section, Elevation, 3D).
 * Output        : Result.Succeeded if at least one Shuffle Colors click applied changes before Close.
 *
 * Notes         :
 * - Reuses CmdFilterProAvailability.CanViewHaveFilters for validation, matching the same gate already
 *   enforced on the ribbon button by CmdColorizeAvailability - no separate/looser validation path.
 * - The window (not this command) owns each apply's Transaction via
 *   GraphicsCommandService.ExecuteSummaryTransaction + ColorizeApplier.ApplyColorizeToViews, exactly
 *   like FilterProWindow owns its own Create/Apply/Shuffle transactions - so Shuffle Colors can be
 *   clicked repeatedly without the window closing, each click its own undo step.
 *
 * Changelog     :
 * v1.0.0 (2026-07-13) - Ported from the standalone AJ Tools tree into the live multi-version src/
 *                       project so the Colorize tool actually gets built and deployed (it previously
 *                       existed only in the stale pre-multiversion copy and could never appear on the
 *                       ribbon no matter how many times the add-in was rebuilt). Behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI;

namespace AJTools.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdColorize : IExternalCommand
    {
        private const string DialogTitle = "Colorize";
        private static bool _isWindowOpen;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;

            if (!ValidateContext(uiDoc, out Document doc, out View activeView, out message))
            {
                TaskDialog.Show(DialogTitle, message);
                return Result.Cancelled;
            }

            if (_isWindowOpen)
            {
                TaskDialog.Show(DialogTitle, "The Colorize window is already open.");
                return Result.Cancelled;
            }

            try
            {
                ColorizeWindow window;

                try
                {
                    _isWindowOpen = true;
                    window = new ColorizeWindow(doc, activeView);
                    new WindowInteropHelper(window)
                    {
                        Owner = commandData.Application.MainWindowHandle
                    };
                    window.ShowDialog();
                }
                finally
                {
                    _isWindowOpen = false;
                }

                return window.HasChanges ? Result.Succeeded : Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show(DialogTitle, $"An unexpected error occurred:\n\n{ex.Message}");
                return Result.Failed;
            }
        }

        private static bool ValidateContext(
            UIDocument uiDoc,
            out Document doc,
            out View activeView,
            out string validationMessage)
        {
            doc = null;
            activeView = null;
            validationMessage = string.Empty;

            if (uiDoc == null || uiDoc.Document == null)
            {
                validationMessage = "Open a project document before running this command.";
                return false;
            }

            doc = uiDoc.Document;

            if (doc.IsReadOnly)
            {
                validationMessage = "The current document is read-only. Please open an editable document.";
                return false;
            }

            if (doc.IsFamilyDocument)
            {
                validationMessage = "Colorize cannot be used in family documents. Please open a project document.";
                return false;
            }

            activeView = uiDoc.ActiveView;

            if (activeView == null)
            {
                validationMessage = "No active view found. Please open a view before running this command.";
                return false;
            }

            if (!CmdFilterProAvailability.CanViewHaveFilters(activeView, out string viewReason))
            {
                validationMessage =
                    $"The current view ({activeView.ViewType}) does not support graphics overrides.\n\n" +
                    $"{viewReason}\n\n" +
                    "Please switch to a view that supports visibility/graphics overrides " +
                    "(e.g. plan, section, elevation, 3D).";
                return false;
            }

            return true;
        }
    }
}
