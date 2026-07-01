#region Metadata
/*
 * Tool Name     : Purge Family Parameters (Purge Unused Family Parameters)
 * File Name     : CmdPurgeUnusedFamilyParameters.cs
 * Purpose       : Opens the Purge Unused Family Parameters window in the Family Editor, which scans family
 *                 parameters, classifies safe-to-remove candidates, and deletes the ones the user selects.
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
 * Dependencies  : Autodesk Revit API, AJTools.UI.Purge (PurgeUnusedFamilyParametersWindow)
 *
 * Input         : Active Family document - the parameters the user selects to remove in the window.
 * Output        : Selected parameters deleted (transaction owned by the window); final report from the window.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Family-Editor-only tool; exits cleanly with a plain message when a project (non-family) document is active.
 * - Deletion is user-confirmed by the explicit selection + Delete action in the window.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-05-11) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Purge behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
            try
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
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
