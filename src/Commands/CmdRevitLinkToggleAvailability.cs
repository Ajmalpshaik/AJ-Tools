#region Metadata
/*
 * Tool Name     : Revit Link Toggle Availability
 * File Name     : CmdRevitLinkToggleAvailability.cs
 * Purpose       : Ribbon availability filter — enables the Toggle Link button only in views
 *                 where the Revit Links category visibility can actually be changed.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-06-30
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active View
 * Output        : Boolean availability state for the ribbon button
 *
 * Notes         :
 * - Runs continuously while Revit is open; kept fast and exception-safe (returns false on any issue).
 * - Mirrors the host command's own gate (View.CanCategoryBeHidden on Revit Links).
 * - Enabled in plan, section, elevation, and 3D views; greyed out in drafting, legend, schedule,
 *   sheet, template, and family views where the Revit Links category is not applicable.
 *
 * Changelog     :
 * v1.0.0 (2026-06-30) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Enables the Toggle Link button only when the Revit Links category can be hidden in the active view.
    /// </summary>
    public class CmdRevitLinkToggleAvailability : IExternalCommandAvailability
    {
        private static readonly ElementId RevitLinksCategoryId = new ElementId(BuiltInCategory.OST_RvtLinks);

        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            try
            {
                UIDocument uiDoc = applicationData?.ActiveUIDocument;
                if (uiDoc?.Document == null)
                    return false;

                if (uiDoc.Document.IsFamilyDocument)
                    return false;

                View view = uiDoc.ActiveView;
                if (view == null || !view.IsValidObject || view.IsTemplate)
                    return false;

                return view.CanCategoryBeHidden(RevitLinksCategoryId);
            }
            catch
            {
                // Any unexpected issue keeps the button disabled rather than crashing the ribbon.
                return false;
            }
        }
    }
}
