#region Metadata
/*
 * Tool Name     : Purge Unplaced Views (availability)
 * File Name     : CmdPurgeUnplacedViewsAvailability.cs
 * Purpose       : Ribbon availability rule shared by eight Purge buttons - the five Purge Unplaced tools
 *                 (3D Views, Sections, Schedules, Legends, Drafting Views) and the three Purge Unused tools
 *                 (View Templates, Filters, Groups) - enables them only when a non-family project
 *                 document is open.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.1
 *
 * Created Date  : 2026-05-11
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active UIApplication (evaluated by Revit on ribbon state changes).
 * Output        : True when a project document is open; false otherwise.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Read-only availability check; makes no model changes.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-05-11) - Ported from an earlier AJ Tools script.
 * v1.1.0 (2026-07-01) - Refactor/audit: standardized metadata block. Availability behaviour unchanged.
 * v1.1.1 (2026-10-12) - Header corrected: it guards eight ribbon buttons, not two. No code change.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    public class CmdPurgeUnplacedViewsAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            UIDocument uiDoc = applicationData?.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                return false;
            }

            return !uiDoc.Document.IsFamilyDocument;
        }
    }
}
