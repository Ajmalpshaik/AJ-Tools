#region Metadata
/*
 * Tool Name     : Purge Family Parameters (availability)
 * File Name     : CmdPurgeUnusedFamilyParametersAvailability.cs
 * Purpose       : Ribbon availability rule for the Purge Family Parameters button - enables it only when
 *                 the active document is a Family document.
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
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active UIApplication (evaluated by Revit on ribbon state changes).
 * Output        : True when a Family document is open; false otherwise.
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Read-only availability check; makes no model changes.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-05-11) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Availability behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
